using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        /// <summary>Lấy danh sách load case (dùng OAPI GetNameList).</summary>
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
            catch { }
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

        /// <summary>Liệt kê toàn bộ bảng database để chẩn đoán (key | name | empty?).</summary>
        public static string ListTables(cSapModel model)
        {
            var sb = new StringBuilder();
            if (model == null) return "cSapModel null";
            try
            {
                cDatabaseTables db = model.DatabaseTables;
                int n = 0; string[] keys = null, names = null; int[] it = null; bool[] isEmpty = null;
                int ret = db.GetAllTables(ref n, ref keys, ref names, ref it, ref isEmpty);
                if (ret != 0 || keys == null) return "Không lấy được danh sách bảng (GetAllTables=" + ret + ").";
                sb.AppendLine("Tổng " + keys.Length + " bảng:");
                for (int i = 0; i < keys.Length; i++)
                {
                    string nm = (names != null && i < names.Length) ? names[i] : "";
                    string em = (isEmpty != null && i < isEmpty.Length && isEmpty[i]) ? "  (rỗng)" : "";
                    sb.AppendLine("- " + keys[i] + (nm.Length > 0 ? "  |  " + nm : "") + em);
                }
            }
            catch (Exception ex) { sb.AppendLine("Lỗi: " + ex.Message); }
            return sb.ToString();
        }

        /// <summary>
        /// Ghi Creep / Shrinkage / Aging vào các load case qua Interactive Database Editing.
        /// Dò bảng có cột chứa 'creep'/'shrink'/'aging', sửa các dòng trùng tên case, rồi Apply.
        /// </summary>
        public static ApplyResult ApplyViaDatabase(cSapModel model, List<string> caseNames,
            double creep, double shrink, double aging)
        {
            var res = new ApplyResult();
            var sb = new StringBuilder();
            if (model == null) { res.Log = "cSapModel null"; return res; }

            try
            {
                cDatabaseTables db = model.DatabaseTables;
                int numTables = 0;
                string[] keys = null, names = null;
                int[] importType = null;
                bool[] isEmpty = null;
                int ret = db.GetAllTables(ref numTables, ref keys, ref names, ref importType, ref isEmpty);
                if (ret != 0 || keys == null)
                {
                    res.Log = "Không lấy được danh sách bảng (GetAllTables=" + ret + ").";
                    return res;
                }

                // Ưu tiên các bảng có tên liên quan; nếu không có thì quét toàn bộ.
                var candidates = new List<int>();
                for (int i = 0; i < keys.Length; i++)
                {
                    if (isEmpty != null && i < isEmpty.Length && isEmpty[i]) continue; // bỏ bảng rỗng
                    string nm = ((names != null && i < names.Length) ? names[i] : keys[i] ?? "").ToLowerInvariant();
                    string ky = (keys[i] ?? "").ToLowerInvariant();
                    if (nm.Contains("load case") || nm.Contains("crack") || nm.Contains("creep")
                        || ky.Contains("crack") || ky.Contains("creep") || ky.Contains("loadcase"))
                        candidates.Add(i);
                }
                if (candidates.Count == 0)
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (isEmpty != null && i < isEmpty.Length && isEmpty[i]) continue;
                        candidates.Add(i);
                    }

                foreach (int idx in candidates)
                {
                    string tableKey = keys[idx];
                    string groupName = "";
                    int tableVersion = 0, numRecords = 0;
                    string[] fieldKeys = null, data = null;
                    try
                    {
                        int r2 = db.GetTableForEditingArray(tableKey, groupName, ref tableVersion,
                            ref fieldKeys, ref numRecords, ref data);
                        if (r2 != 0 || fieldKeys == null || data == null || fieldKeys.Length == 0) continue;
                    }
                    catch { continue; }

                    int numFields = fieldKeys.Length;
                    int creepCol = FindField(fieldKeys, "creep");
                    int shrinkCol = FindField(fieldKeys, "shrink");
                    int agingCol = FindField(fieldKeys, "aging", "age");
                    if (creepCol < 0 && shrinkCol < 0) continue; // không phải bảng cần tìm

                    int caseCol = FindField(fieldKeys, "case", "name");
                    if (caseCol < 0) caseCol = 0;

                    sb.AppendLine("Bảng khớp: " + tableKey);
                    sb.AppendLine("Cột: " + string.Join(", ", fieldKeys));

                    int updated = 0;
                    for (int rIdx = 0; rIdx < numRecords; rIdx++)
                    {
                        int baseI = rIdx * numFields;
                        if (baseI + numFields > data.Length) break;
                        string cn = data[baseI + caseCol];
                        if (cn == null) continue;
                        if (!caseNames.Any(x => string.Equals(x, cn, StringComparison.OrdinalIgnoreCase))) continue;
                        if (creepCol >= 0) data[baseI + creepCol] = creep.ToString(CultureInfo.InvariantCulture);
                        if (shrinkCol >= 0) data[baseI + shrinkCol] = shrink.ToString(CultureInfo.InvariantCulture);
                        if (agingCol >= 0) data[baseI + agingCol] = aging.ToString(CultureInfo.InvariantCulture);
                        updated++;
                    }

                    if (updated == 0)
                    {
                        sb.AppendLine("Không có dòng nào khớp tên case trong bảng này.");
                        continue;
                    }

                    int r3 = db.SetTableForEditingArray(tableKey, ref tableVersion, ref fieldKeys, numRecords, ref data);
                    int nFatal = 0, nErr = 0, nWarn = 0, nInfo = 0; string importLog = "";
                    int r4 = db.ApplyEditedTables(true, ref nFatal, ref nErr, ref nWarn, ref nInfo, ref importLog);
                    sb.AppendLine("Cập nhật " + updated + " dòng. Set=" + r3 + ", Apply=" + r4 +
                                  " (Fatal=" + nFatal + ", Err=" + nErr + ", Warn=" + nWarn + ")");
                    if (!string.IsNullOrWhiteSpace(importLog)) sb.AppendLine(importLog.Trim());

                    if (r3 == 0 && r4 == 0 && nFatal == 0 && nErr == 0)
                    {
                        res.Success = true;
                        res.MethodUsed = "DatabaseTables: " + tableKey;
                        break;
                    }
                }

                if (!res.Success && sb.Length == 0)
                    sb.AppendLine("Không tìm thấy bảng nào chứa cột creep/shrinkage. " +
                                  "Hãy bấm 'Liệt kê bảng' và gửi log để xác định đúng bảng.");
            }
            catch (Exception ex) { sb.AppendLine("Lỗi: " + ex.Message); }

            res.Log = sb.ToString();
            return res;
        }

        private static int FindField(string[] fields, params string[] kws)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                string f = (fields[i] ?? "").ToLowerInvariant();
                foreach (var k in kws) if (f.Contains(k)) return i;
            }
            return -1;
        }
    }
}
