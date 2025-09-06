using System;
using System.Windows.Forms;

namespace EzYTDlp_V2
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 檢查 ffmpeg 是否存在於 PATH
            bool hasFfmpeg = CheckFfmpegInPath();
            Properties.Settings.Default.HasFfmpeg = hasFfmpeg;
            Properties.Settings.Default.Save();

            Application.Run(new Launcher());
        }

        private static bool CheckFfmpegInPath()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH");
                string[] directories = path.Split(';');
                foreach (string dir in directories)
                {
                    string ffmpegPath = System.IO.Path.Combine(dir, "ffmpeg.exe");
                    if (System.IO.File.Exists(ffmpegPath))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}