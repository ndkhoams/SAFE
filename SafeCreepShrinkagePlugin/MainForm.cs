using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using SAFEv1;

namespace SafeCreepShrinkagePlugin
{
    /// <summary>Giao diện chính của plugin.</summary>
    public class MainForm : Form
    {
        private readonly cSapModel _model;

        private TextBox txtFck, txtAc, txtU, txtRH, txtT0, txtTemp, txtTs, txtAging;
        private ComboBox cmbCement;
        private Label lblResult;
        private CheckedListBox clbCases;
        private TextBox txtLog;
        private CreepShrinkageResult _last;

        public MainForm(cSapModel model)
        {
            _model = model;
            BuildUi();
            LoadCasesIntoList();
        }

        private void BuildUi()
        {
            Text = "Creep & Shrinkage (EC2) - SAFE 22";
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 600);
            MinimumSize = new Size(820, 600);

            // ----- Nhóm đầu vào -----
            var grpIn = new GroupBox { Text = "Dữ liệu đầu vào (EN 1992-1-1)", Left = 12, Top = 8, Width = 380, Height = 300 };
            int y = 26;
            txtFck = AddRow(grpIn, "f_ck [MPa]", "25", ref y);
            txtAc = AddRow(grpIn, "A_c [m²]", "0.22", ref y);
            txtU = AddRow(grpIn, "u - chu vi khô [m]", "2.0", ref y);
            txtRH = AddRow(grpIn, "RH [%]", "75", ref y);
            txtT0 = AddRow(grpIn, "t₀ - tuổi chất tải [ngày]", "28", ref y);
            txtTemp = AddRow(grpIn, "T - nhiệt độ [°C]", "30", ref y);
            txtTs = AddRow(grpIn, "t_s - bắt đầu khô [ngày]", "7", ref y);

            var lblCem = new Label { Text = "Loại xi măng", Left = 14, Top = y + 3, Width = 170 };
            cmbCement = new ComboBox { Left = 190, Top = y, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCement.Items.AddRange(new object[] { "S - chậm đông cứng", "N - bình thường", "R - nhanh đông cứng" });
            cmbCement.SelectedIndex = 1;
            grpIn.Controls.Add(lblCem);
            grpIn.Controls.Add(cmbCement);
            y += 30;

            var btnCalc = new Button { Text = "Tính", Left = 190, Top = y + 6, Width = 170, Height = 30 };
            btnCalc.Click += (s, e) => Calculate();
            grpIn.Controls.Add(btnCalc);
            Controls.Add(grpIn);

            // ----- Nhóm kết quả -----
            var grpRes = new GroupBox { Text = "Kết quả (giá trị cuối t → ∞)", Left = 404, Top = 8, Width = 404, Height = 300 };
            lblResult = new Label { Left = 14, Top = 24, Width = 376, Height = 264, Font = new Font("Consolas", 9f) };
            grpRes.Controls.Add(lblResult);
            Controls.Add(grpRes);

            // ----- Load cases -----
            var grpCases = new GroupBox { Text = "Load case long-term (tích để áp giá trị)", Left = 12, Top = 314, Width = 380, Height = 230 };
            clbCases = new CheckedListBox { Left = 14, Top = 22, Width = 352, Height = 150, CheckOnClick = true };
            grpCases.Controls.Add(clbCases);

            var lblAging = new Label { Text = "Aging coef.", Left = 14, Top = 180, Width = 80 };
            txtAging = new TextBox { Left = 96, Top = 177, Width = 60, Text = "0.8" };
            var btnApply = new Button { Text = "Áp vào load case", Left = 166, Top = 175, Width = 130, Height = 28 };
            btnApply.Click += (s, e) => ApplyToCases();
            var btnReload = new Button { Text = "Tải lại", Left = 300, Top = 175, Width = 66, Height = 28 };
            btnReload.Click += (s, e) => LoadCasesIntoList();
            grpCases.Controls.Add(lblAging);
            grpCases.Controls.Add(txtAging);
            grpCases.Controls.Add(btnApply);
            grpCases.Controls.Add(btnReload);
            Controls.Add(grpCases);

            // ----- Log -----
            var grpLog = new GroupBox { Text = "Nhật ký / hướng dẫn", Left = 404, Top = 314, Width = 404, Height = 230 };
            txtLog = new TextBox { Left = 14, Top = 22, Width = 376, Height = 196, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            grpLog.Controls.Add(txtLog);
            Controls.Add(grpLog);

            // ----- Nút dưới -----
            var btnCopy = new Button { Text = "Copy giá trị", Left = 404, Top = 552, Width = 120, Height = 32 };
            btnCopy.Click += (s, e) => CopyValues();
            var btnTables = new Button { Text = "Liệt kê bảng", Left = 532, Top = 552, Width = 130, Height = 32 };
            btnTables.Click += (s, e) => ShowTables();
            var btnClose = new Button { Text = "Đóng", Left = 688, Top = 552, Width = 120, Height = 32 };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnCopy);
            Controls.Add(btnTables);
            Controls.Add(btnClose);
        }

        private TextBox AddRow(Control parent, string label, string def, ref int y)
        {
            var lbl = new Label { Text = label, Left = 14, Top = y + 3, Width = 170 };
            var txt = new TextBox { Left = 190, Top = y, Width = 170, Text = def };
            parent.Controls.Add(lbl);
            parent.Controls.Add(txt);
            y += 30;
            return txt;
        }

        private static double ParseNum(TextBox t, string name)
        {
            string s = (t.Text ?? "").Trim().Replace(',', '.');
            if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                throw new FormatException("Giá trị không hợp lệ: " + name);
            return v;
        }

        private CementClass SelectedCement()
        {
            switch (cmbCement.SelectedIndex) { case 0: return CementClass.S; case 2: return CementClass.R; default: return CementClass.N; }
        }

        private void Calculate()
        {
            try
            {
                var inp = new CreepShrinkageInput
                {
                    Fck = ParseNum(txtFck, "f_ck"),
                    Ac = ParseNum(txtAc, "A_c") * 1e6,   // m² -> mm²
                    U = ParseNum(txtU, "u") * 1e3,        // m -> mm
                    RH = ParseNum(txtRH, "RH"),
                    T0 = ParseNum(txtT0, "t0"),
                    Temp = ParseNum(txtTemp, "T"),
                    Ts = ParseNum(txtTs, "ts"),
                    Cement = SelectedCement(),
                    T = double.PositiveInfinity
                };
                _last = EC2CreepShrinkage.Compute(inp);
                lblResult.Text = FormatResult(_last);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi nhập liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string FormatResult(CreepShrinkageResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("f_cm        = " + r.Fcm.ToString("0.00") + " MPa");
            sb.AppendLine("h₀          = " + r.H0.ToString("0.0") + " mm");
            sb.AppendLine("t₀ (h.chỉnh) = " + r.T0Adjusted.ToString("0.0") + " ngày");
            sb.AppendLine("β(f_cm)     = " + r.BetaFcm.ToString("0.0000"));
            sb.AppendLine("β(t₀)       = " + r.BetaT0.ToString("0.0000"));
            sb.AppendLine("φ_RH        = " + r.PhiRH.ToString("0.0000"));
            sb.AppendLine("φ₀          = " + r.Phi0.ToString("0.0000"));
            sb.AppendLine("β_H         = " + r.BetaH.ToString("0.0"));
            sb.AppendLine("-------------------------------------");
            sb.AppendLine("φ(∞,t₀)     = " + r.Phi.ToString("0.000") + "   << Creep Coefficient");
            sb.AppendLine("-------------------------------------");
            sb.AppendLine("ε_cd0       = " + (r.EpsCd0 * 1e6).ToString("0.0") + " ×10⁻⁶");
            sb.AppendLine("k_h         = " + r.Kh.ToString("0.000"));
            sb.AppendLine("ε_cd        = " + (r.EpsCd * 1e6).ToString("0.0") + " ×10⁻⁶");
            sb.AppendLine("ε_ca        = " + (r.EpsCa * 1e6).ToString("0.0") + " ×10⁻⁶");
            sb.AppendLine("-------------------------------------");
            sb.AppendLine("ε_cs        = " + (r.EpsCs * 1e6).ToString("0.00") + " ×10⁻⁶");
            sb.AppendLine("            = " + r.EpsCs.ToString("0.000000") + "   << Shrinkage Strain");
            return sb.ToString();
        }

        private void LoadCasesIntoList()
        {
            clbCases.Items.Clear();
            var cases = SafeModelHelper.GetLoadCases(_model);
            if (cases.Count == 0)
            {
                Log("Không đọc được load case (mô hình rỗng hoặc OAPI thay đổi chữ ký).");
                return;
            }
            foreach (var c in cases)
                clbCases.Items.Add(c.Name, c.LikelyLongTerm);
            Log("Đã tải " + cases.Count + " load case. Các case nghi là long-term được tích sẵn.");
        }

        private void ApplyToCases()
        {
            if (_last == null) { MessageBox.Show("Hãy bấm Tính trước.", "Thông báo"); return; }
            if (clbCases.CheckedItems.Count == 0) { MessageBox.Show("Chưa chọn load case nào.", "Thông báo"); return; }

            double aging;
            try { aging = ParseNum(txtAging, "aging"); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi"); return; }

            var names = new List<string>();
            foreach (var item in clbCases.CheckedItems) names.Add(item.ToString());

            var res = SafeModelHelper.ApplyViaDatabase(_model, names, _last.Phi, _last.EpsCs, aging);
            var sb = new StringBuilder();
            sb.AppendLine(res.Success ? ("THÀNH CÔNG qua " + res.MethodUsed) : "CHƯ!A ghi được qua OAPI.");
            if (!string.IsNullOrEmpty(res.Log)) sb.AppendLine(res.Log.TrimEnd());
            sb.AppendLine("---");
            sb.AppendLine("Creep = " + _last.Phi.ToString("0.000") + " ; Shrinkage = " + _last.EpsCs.ToString("0.000000") + " ; Aging = " + aging.ToString("0.00"));
            if (!res.Success)
                sb.AppendLine("Nếu chưa ghi được: bấm 'Liệt kê bảng' và gửi log, hoặc dùng Copy để nhập tay vào Load Case Data (Nonlinear Long Term Cracked).");
            Log(sb.ToString());
        }

        private void ShowTables()
        {
            Log(SafeModelHelper.ListTables(_model));
        }

        private void CopyValues()
        {
            if (_last == null) { MessageBox.Show("Hãy bấm Tính trước.", "Thông báo"); return; }
            double aging = 0.8;
            try { aging = ParseNum(txtAging, "aging"); } catch { }
            string text = "Creep Coefficient = " + _last.Phi.ToString("0.000") +
                          "\r\nShrinkage Strain = " + _last.EpsCs.ToString("0.000000") +
                          "\r\nAging Coefficient = " + aging.ToString("0.00");
            try { Clipboard.SetText(text); Log("Đã copy:\r\n" + text); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Lỗi copy"); }
        }

        private void Log(string msg)
        {
            txtLog.Text = msg + "\r\n";
        }
    }
}
