using System;
using System.Drawing;
using System.Windows.Forms;

namespace EzYTDlp_V2
{
    public partial class Settings : Form
    {
        private Label ffmpegWarning;

        public Settings()
        {
            InitializeComponent();

            try
            {
                checkBox1.Checked = Properties.Settings.Default.DownloadSound;
                checkBox2.Checked = Properties.Settings.Default.AutoConvert;
                checkBox3.Checked = Properties.Settings.Default.DownloadNotify;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (!Properties.Settings.Default.HasFfmpeg)
            {
                checkBox2.Checked = false;
                checkBox2.Enabled = false;

                ffmpegWarning = new Label
                {
                    Text = "Your computer does not have ffmpeg!",
                    ForeColor = Color.Red,
                    Location = new Point(12, 80),
                    AutoSize = true
                };
                this.Controls.Add(ffmpegWarning);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            // 如有需要額外邏輯
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                Properties.Settings.Default.DownloadSound = checkBox1.Checked;
                Properties.Settings.Default.AutoConvert = checkBox2.Checked;
                Properties.Settings.Default.DownloadNotify = checkBox3.Checked;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.Hide();
            new Launcher().ShowDialog();
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Hide();
            new Launcher().ShowDialog();
            this.Close();
        }
    }
}