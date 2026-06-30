using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SAFEv1;

namespace SafeCreepShrinkagePlugin
{
    /// <summary>Thông tin tóm tắt một load case trong mô hình.</summary>
    public class LoadCaseInfo
    {
        public string Name;
        public bool LikelyLongTerm;
    }

    /// <summary>Kết quả của thao tác ghi giá trị vào load case.</summary>
    public class ApplyResult
    {
        public bool Success;
        public string MethodUsed = "";
        public string Log = "";
    }

    /// <summary>Các tiện ích làm việc với cSapModel của SAFE.</summary>
    public static class SafeModelHelper
    {
        /// <summary>Lấy danh sách load case (dùng OAPI GetNameList - đáng tin cậy).</summary>
        public static List<LoadCaseInfo> GetLoadCases(cSapModel model)
        {
            var list = new List<LoadCaseInfo>();
            if (model == null) return list;
            int num = 0;
            string[] names = null;
            try
            {
                int ret = model.LoadCases.GetNameList(ref num, ref names);
                if (ret != 0 || names == null) return list;
                foreach (var n in names)
                    list.Add(new LoadCaseInfo { Name = n, LikelyLongTerm = IsLikelyLongTerm(n) });
            }
            catch
            {
                // nếu OAPI đổi chữ ký, trả danh sách rỗng để UI báo.
            }
            return list;
        }

        private static bool IsLikelyLongTerm(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string s = name.ToLowerInvariant();
            return s.Contains("long") || s.Contains("term") || s.Contains("creep")
                || s.Contains("crack") || s.Contains("sustain") || s.Contains("lt")
                || s.Contains("dai han") || s.Contains("dài hạn");
        }

        /// <summary>
        /// Cố gắng ghi Creep / Shrinkage / Aging vào một load case qua OAPI bằng reflection.
        /// OAPI của SAFE không phải lúc nào cũng phơi bày các tham số này, nên hàm
        /// dò các phương thức Set...Crack/Creep/Shrink/LongTerm và gọi nếu chữ ký phù hợp.
        /// </summary>
        public static ApplyResult TryApplyCreepShrinkage(cSapModel model, string caseName,
            double creep, double shrinkage, double aging)
        {
            var res = new ApplyResult();
            var sb = new StringBuilder();
            if (model == null) { res.Log = "cSapModel null"; return res; }

            try
            {
                object loadCases = model.LoadCases;
                var targets = new List<object> { loadCases };
                foreach (var p in loadCases.GetType().GetProperties())
                {
                    try { var v = p.GetValue(loadCases, null); if (v != null) targets.Add(v); }
                    catch { }
                }

                foreach (var target in targets)
                {
                    foreach (var m in target.GetType().GetMethods())
                    {
                        string mn = m.Name.ToLowerInvariant();
                        bool relevant = mn.StartsWith("set") &&
                            (mn.Contains("crack") || mn.Contains("creep") || mn.Contains("shrink")
                             || mn.Contains("longterm") || mn.Contains("timedep"));
                        if (!relevant) continue;

                        var ps = m.GetParameters();
                        if (ps.Length == 0 || ps[0].ParameterType != typeof(string))
                        {
                            sb.AppendLine("Bỏ qua (không nhận tên case): " + target.GetType().Name + "." + m.Name);
                            continue;
                        }

                        object[] args = new object[ps.Length];
                        args[0] = caseName;
                        for (int i = 1; i < ps.Length; i++)
                        {
                            var pt = ps[i].ParameterType;
                            string pn = (ps[i].Name ?? "").ToLowerInvariant();
                            if (pt == typeof(double) || pt == typeof(float))
                            {
                                if (pn.Contains("creep")) args[i] = creep;
                                else if (pn.Contains("shrink")) args[i] = shrinkage;
                                else if (pn.Contains("age") || pn.Contains("aging")) args[i] = aging;
                                else args[i] = 0.0;
                            }
                            else if (pt == typeof(bool)) args[i] = true;
                            else if (pt == typeof(int)) args[i] = 0;
                            else if (pt == typeof(string)) args[i] = "";
                            else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                        }

                        try
                        {
                            object ret = m.Invoke(target, args);
                            int code = (ret is int) ? (int)ret : 0;
                            sb.AppendLine("Gọi " + target.GetType().Name + "." + m.Name + " -> mã " + code);
                            if (code == 0) { res.Success = true; res.MethodUsed = target.GetType().Name + "." + m.Name; }
                        }
                        catch (Exception ex)
                        {
                            var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                            sb.AppendLine("Gọi " + m.Name + " thất bại: " + inner);
                        }
                    }
                }

                if (!res.Success)
                    sb.AppendLine("Không gọi thành công phương thức OAPI nào cho tham số cracking long-term. " +
                                  "Hãy nhập tay giá trị bên dưới vào hộp thoại Load Case Data.");
            }
            catch (Exception ex)
            {
                sb.AppendLine("Lỗi: " + ex.Message);
            }

            res.Log = sb.ToString();
            return res;
        }
    }
}
