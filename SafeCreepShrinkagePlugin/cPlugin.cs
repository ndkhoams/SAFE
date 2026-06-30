using System;
using System.Windows.Forms;
using SAFEv1;

namespace SafeCreepShrinkagePlugin
{
    /// <summary>
    /// Điểm vào plugin cho SAFE 22 (CSI OAPI). SAFE gọi Main() khi người dùng chạy
    /// plugin từ menu Tools. Tên lớp đầy đủ cần khai báo khi thêm plugin:
    /// SafeCreepShrinkagePlugin.cPlugin
    /// </summary>
    public class cPlugin : cPluginContract
    {
        public void Main(ref cSapModel SapModel, ref cPluginCallback ISapPlugin)
        {
            int ret = 0;
            try
            {
                if (SapModel == null)
                {
                    MessageBox.Show("Không lấy được cSapModel từ SAFE.", "Creep & Shrinkage EC2",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    using (var form = new MainForm(SapModel))
                    {
                        form.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                ret = 1;
                MessageBox.Show("Lỗi plugin: " + ex.Message, "Creep & Shrinkage EC2",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // BẮT BUỘC: trả quyền điều khiển lại cho SAFE.
                if (ISapPlugin != null) ISapPlugin.Finish(ret);
            }
        }

        public int Info(ref string Text)
        {
            Text = "Tính hệ số từ biến (creep) và biến dạng co ngót (shrinkage) theo EN 1992-1-1 (EC2) " +
                   "và áp vào các load case long-term (Nonlinear Long Term Cracked) trong SAFE.";
            return 0;
        }
    }
}
