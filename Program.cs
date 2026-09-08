using System;
using System.Threading;
using System.Windows.Forms;

namespace LightweightAutoClicker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "LightweightAutoClicker.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("程序已经在运行。", "AUTO CLICKER  by：一叶丶知秋", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
