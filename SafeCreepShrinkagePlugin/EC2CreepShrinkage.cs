using System;

namespace SafeCreepShrinkagePlugin
{
    /// <summary>Loại xi măng theo EN 1992-1-1 (ảnh hưởng α của tuổi bê tông và co ngót khô).</summary>
    public enum CementClass
    {
        /// <summary>Chậm đông cứng (vd CEM 32.5 N): α=-1, α_ds1=3, α_ds2=0.13.</summary>
        S,
        /// <summary>Bình thường (vd CEM 32.5 R, 42.5 N): α=0, α_ds1=4, α_ds2=0.12.</summary>
        N,
        /// <summary>Nhanh đông cứng (vd CEM 42.5 R, 52.5 N/R): α=+1, α_ds1=6, α_ds2=0.11.</summary>
        R
    }

    /// <summary>Dữ liệu đầu vào (đơn vị: A_c [mm²], u [mm], f_ck [MPa], tuổi [ngày]).</summary>
    public class CreepShrinkageInput
    {
        public double Fck;            // MPa
        public double Ac;             // mm^2
        public double U;              // mm (chu vi tiếp xúc môi trường khô)
        public double RH = 50.0;      // %
        public double T0 = 28.0;      // ngày - tuổi khi chất tải
        public double Temp = 20.0;    // °C - nhiệt độ môi trường
        public double Ts = 7.0;       // ngày - tuổi bắt đầu khô (kết thúc bảo dưỡng)
        public CementClass Cement = CementClass.N;
        /// <summary>Tuổi để tính [ngày]. double.PositiveInfinity = giá trị cuối (t → ∞).</summary>
        public double T = double.PositiveInfinity;
    }

    /// <summary>Kết quả tính, kèm các giá trị trung gian để kiểm chứng.</summary>
    public class CreepShrinkageResult
    {
        public double Fcm;
        public double H0;
        public double T0Adjusted;
        public double BetaFcm;
        public double BetaT0;
        public double PhiRH;
        public double Phi0;
        public double BetaH;
        public double BetaC;
        public double Phi;        // hệ số từ biến φ(t,t0)

        public double EpsCd0;
        public double Kh;
        public double BetaDs;
        public double EpsCd;      // co ngót khô
        public double EpsCaInf;
        public double BetaAs;
        public double EpsCa;      // co ngót tự sinh
        public double EpsCs;      // tổng biến dạng co ngót
    }

    /// <summary>Tính hệ số từ biến và biến dạng co ngót theo EN 1992-1-1 (mục 3.1.4 + Phụ lục B).</summary>
    public static class EC2CreepShrinkage
    {
        public static CreepShrinkageResult Compute(CreepShrinkageInput inp)
        {
            if (inp == null) throw new ArgumentNullException(nameof(inp));
            if (inp.Fck <= 0) throw new ArgumentException("f_ck phải > 0");
            if (inp.Ac <= 0) throw new ArgumentException("A_c phải > 0");
            if (inp.U <= 0) throw new ArgumentException("u (chu vi khô) phải > 0");
            if (inp.RH <= 0 || inp.RH >= 100) throw new ArgumentException("RH phải trong khoảng (0, 100)");
            if (inp.T0 <= 0) throw new ArgumentException("t0 phải > 0");

            var r = new CreepShrinkageResult();
            double fck = inp.Fck;
            double fcm = fck + 8.0;
            r.Fcm = fcm;

            double h0 = 2.0 * inp.Ac / inp.U; // mm
            r.H0 = h0;

            // ---------- HỆ SỐ TỪ BIẾN (Annex B) ----------
            // Tuổi hiệu chỉnh nhiệt độ (B.10) - giả thiết nhiệt độ không đổi.
            double tT = Math.Exp(-(4000.0 / (273.0 + inp.Temp) - 13.65)) * inp.T0;
            // Hiệu chỉnh loại xi măng (B.9).
            double alphaCem = AlphaCement(inp.Cement);
            double t0 = tT * Math.Pow(9.0 / (2.0 + Math.Pow(tT, 1.2)) + 1.0, alphaCem);
            if (t0 < 0.5) t0 = 0.5;
            r.T0Adjusted = t0;

            double betaFcm = 16.8 / Math.Sqrt(fcm);
            r.BetaFcm = betaFcm;
            double betaT0 = 1.0 / (0.1 + Math.Pow(t0, 0.20));
            r.BetaT0 = betaT0;

            double a1 = Math.Pow(35.0 / fcm, 0.7);
            double a2 = Math.Pow(35.0 / fcm, 0.2);
            double a3 = Math.Pow(35.0 / fcm, 0.5);
            double term = (1.0 - inp.RH / 100.0) / (0.1 * Math.Pow(h0, 1.0 / 3.0));
            double phiRH = (fcm <= 35.0) ? (1.0 + term) : ((1.0 + term * a1) * a2);
            r.PhiRH = phiRH;

            double phi0 = phiRH * betaFcm * betaT0;
            r.Phi0 = phi0;

            double rhTerm = Math.Pow(0.012 * inp.RH, 18);
            double betaH = (fcm <= 35.0)
                ? Math.Min(1.5 * (1.0 + rhTerm) * h0 + 250.0, 1500.0)
                : Math.Min(1.5 * (1.0 + rhTerm) * h0 + 250.0 * a3, 1500.0 * a3);
            r.BetaH = betaH;

            double betaC;
            if (double.IsPositiveInfinity(inp.T))
                betaC = 1.0;
            else
            {
                double dt = Math.Max(inp.T - inp.T0, 0.0);
                betaC = Math.Pow(dt / (betaH + dt), 0.3);
            }
            r.BetaC = betaC;
            r.Phi = phi0 * betaC;

            // ---------- CO NGÓT (3.1.4) ----------
            double alphaDs1 = AlphaDs1(inp.Cement);
            double alphaDs2 = AlphaDs2(inp.Cement);
            double betaRH = 1.55 * (1.0 - Math.Pow(inp.RH / 100.0, 3));
            double epsCd0 = 0.85 * ((220.0 + 110.0 * alphaDs1) * Math.Exp(-alphaDs2 * fcm / 10.0)) * 1e-6 * betaRH;
            r.EpsCd0 = epsCd0;
            double kh = Kh(h0);
            r.Kh = kh;

            double betaDs;
            if (double.IsPositiveInfinity(inp.T))
                betaDs = 1.0;
            else
            {
                double dts = Math.Max(inp.T - inp.Ts, 0.0);
                double denom = dts + 0.04 * Math.Sqrt(h0 * h0 * h0);
                betaDs = denom > 0 ? dts / denom : 0.0;
            }
            r.BetaDs = betaDs;
            r.EpsCd = betaDs * kh * epsCd0;

            double epsCaInf = fck > 10.0 ? 2.5 * (fck - 10.0) * 1e-6 : 0.0;
            r.EpsCaInf = epsCaInf;
            double betaAs = double.IsPositiveInfinity(inp.T)
                ? 1.0
                : 1.0 - Math.Exp(-0.2 * Math.Sqrt(Math.Max(inp.T, 0.0)));
            r.BetaAs = betaAs;
            r.EpsCa = betaAs * epsCaInf;

            r.EpsCs = r.EpsCd + r.EpsCa;
            return r;
        }

        private static double AlphaCement(CementClass c)
        {
            switch (c) { case CementClass.S: return -1.0; case CementClass.R: return 1.0; default: return 0.0; }
        }
        private static double AlphaDs1(CementClass c)
        {
            switch (c) { case CementClass.S: return 3.0; case CementClass.R: return 6.0; default: return 4.0; }
        }
        private static double AlphaDs2(CementClass c)
        {
            switch (c) { case CementClass.S: return 0.13; case CementClass.R: return 0.11; default: return 0.12; }
        }
        /// <summary>Nội suy k_h theo h0 [mm]: (100,1.0)(200,0.85)(300,0.75)(≥500,0.70).</summary>
        private static double Kh(double h0)
        {
            if (h0 <= 100.0) return 1.00;
            if (h0 >= 500.0) return 0.70;
            double[] x = { 100.0, 200.0, 300.0, 500.0 };
            double[] y = { 1.00, 0.85, 0.75, 0.70 };
            for (int i = 0; i < x.Length - 1; i++)
                if (h0 >= x[i] && h0 <= x[i + 1])
                    return y[i] + (y[i + 1] - y[i]) * (h0 - x[i]) / (x[i + 1] - x[i]);
            return 0.70;
        }
    }
}
