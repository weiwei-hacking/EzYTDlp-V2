using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EzYTDlp_V2
{
    public partial class Main : Form
    {
        // 星空
        private Star[] stars;
        private Timer starTimer;
        private Random rand = new Random();

        private string ytdlpPath;
        private string currentUrl;
        private string thumbnailUrl;
        private string savePath;
        private string videoTitle; // 用於儲存乾淨標題
        private Process currentDownloadProcess; // 追蹤下載進程
        private Process currentConvertProcess;  // 追蹤轉檔進程
        private bool isDownloading;             // 標記是否正在下載/轉檔

        // P/Invoke for suspending and resuming process
        [DllImport("ntdll.dll")]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        private static extern int NtResumeProcess(IntPtr processHandle);

        public Main()
        {
            InitializeComponent();

            this.Text = "Waiting link...";
            pictureBox1.Visible = false;
            textBox1.TextChanged += new EventHandler(textBox1_TextChanged);
            this.FormClosing += new FormClosingEventHandler(Main_FormClosing);
            ExtractYtdlp();

            // 初始化星空
            InitStars();
        }

        // 初始化星空
        private void InitStars()
        {
            stars = new Star[100]; // 星星數量
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new Star(rand, this.ClientSize);
            }

            starTimer = new Timer();
            starTimer.Interval = 30; // 更新間隔
            starTimer.Tick += (s, e) =>
            {
                foreach (var star in stars)
                    star.Update(this.ClientSize);
                this.Invalidate();
            };
            starTimer.Start();
        }

        // 繪製背景
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            e.Graphics.Clear(Color.Black); // 黑色背景
            foreach (var star in stars)
                star.Draw(e.Graphics);
        }

        // 星星類別
        private class Star
        {
            private int x, y;
            private int size;
            private int speed;
            private int brightness;
            private int delta;
            private Random rand;

            public Star(Random rand, Size bounds)
            {
                this.rand = rand;
                Reset(bounds);
            }

            public void Update(Size bounds)
            {
                // 閃爍
                brightness += delta;

                if (brightness <= 100 || brightness >= 245)
                {
                    delta *= -1;

                    // 這行確保 brightness 永遠在 [100, 255] 範圍
                    brightness = Math.Max(100, Math.Min(245, brightness));
                }

                // 移動 (往下飄)
                y += speed;
                if (y > bounds.Height)
                    Reset(bounds);
            }

            public void Draw(Graphics g)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(brightness, Color.White)))
                {
                    g.FillEllipse(brush, x, y, size, size);
                }
            }

            private void Reset(Size bounds)
            {
                x = rand.Next(bounds.Width);
                y = rand.Next(bounds.Height);
                size = rand.Next(1, 4);
                speed = rand.Next(1, 4);
                brightness = rand.Next(100, 245);
                delta = rand.Next(0, 2) == 0 ? 5 : -5;
            }
        }

        // ================= 原本功能區 =================

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isDownloading)
            {
                SuspendProcess(currentDownloadProcess);
                SuspendProcess(currentConvertProcess);

                DialogResult result = MessageBox.Show("Are you sure to close the window with unfinished tasks?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    if (currentDownloadProcess != null && !currentDownloadProcess.HasExited)
                        currentDownloadProcess.Kill();
                    if (currentConvertProcess != null && !currentConvertProcess.HasExited)
                        currentConvertProcess.Kill();
                }
                else
                {
                    e.Cancel = true;
                    ResumeProcess(currentDownloadProcess);
                    ResumeProcess(currentConvertProcess);
                }
            }
        }

        private void SuspendProcess(Process process)
        {
            if (process != null && !process.HasExited)
                NtSuspendProcess(process.Handle);
        }

        private void ResumeProcess(Process process)
        {
            if (process != null && !process.HasExited)
                NtResumeProcess(process.Handle);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            progressBar1.Value = 0;
            progressBar1.BackColor = SystemColors.Control;
            Graphics g = progressBar1.CreateGraphics();
            g.Clear(progressBar1.BackColor);
        }

        private void ExtractYtdlp()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                ytdlpPath = Path.Combine(tempDir, "yt-dlp.exe");
                if (!File.Exists(ytdlpPath))
                {
                    File.WriteAllBytes(ytdlpPath, Properties.Resources.yt_dlp);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to extract yt-dlp: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Cancel")
            {
                // 取消下載/轉檔
                if (isDownloading)
                {
                    // 先暫停進程
                    SuspendProcess(currentDownloadProcess);
                    SuspendProcess(currentConvertProcess);

                    DialogResult result = MessageBox.Show($"Are you sure to cancel the download {videoTitle}?", "Cancel Download", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        if (currentDownloadProcess != null && !currentDownloadProcess.HasExited)
                        {
                            currentDownloadProcess.Kill();
                        }
                        if (currentConvertProcess != null && !currentConvertProcess.HasExited)
                        {
                            currentConvertProcess.Kill();
                        }
                        this.Invoke(new Action(() =>
                        {
                            progressBar1.Value = 0;
                            progressBar1.BackColor = Color.Red;
                            ResetUIState();
                            button1.Text = "Confirm";
                        }));
                    }
                    else
                    {
                        // 恢復進程
                        ResumeProcess(currentDownloadProcess);
                        ResumeProcess(currentConvertProcess);
                    }
                }
                return;
            }

            currentUrl = textBox1.Text.Trim();
            if (!currentUrl.StartsWith("http://") && !currentUrl.StartsWith("https://"))
            {
                MessageBox.Show("You only can use http or https link", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            textBox1.Enabled = false;
            this.Text = "Link checking...";

            await Task.Run(() =>
            {
                Process checkProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = $"--no-playlist --dump-json \"{currentUrl}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    checkProcess.Start();
                    string output = checkProcess.StandardOutput.ReadToEnd();
                    string error = checkProcess.StandardError.ReadToEnd();
                    checkProcess.WaitForExit();

                    if (checkProcess.ExitCode != 0)
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show($"This link is not allowed: {error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            this.Text = "Waiting link...";
                        }));
                        return;
                    }

                    Regex titleRegex = new Regex("\"title\": \"(.*?)\"");
                    Regex thumbRegex = new Regex("\"thumbnail\": \"(.*?)\"");
                    var titleMatch = titleRegex.Match(output);
                    var thumbMatch = thumbRegex.Match(output);

                    this.Invoke(new Action(() =>
                    {
                        if (titleMatch.Success)
                        {
                            string rawTitle = titleMatch.Groups[1].Value;
                            string decodedTitle = Regex.Unescape(rawTitle);
                            videoTitle = SanitizeFileName(RemoveEmoji(decodedTitle));
                            this.Text = string.IsNullOrEmpty(videoTitle) ? "Waiting link..." : videoTitle;
                        }
                        else
                        {
                            this.Text = "Waiting link...";
                        }

                        if (thumbMatch.Success)
                        {
                            thumbnailUrl = thumbMatch.Groups[1].Value;
                            try
                            {
                                using (WebClient client = new WebClient())
                                {
                                    byte[] imageBytes = client.DownloadData(thumbnailUrl);
                                    using (MemoryStream ms = new MemoryStream(imageBytes))
                                    {
                                        Image originalImage = Image.FromStream(ms);
                                        Image resizedImage = new Bitmap(originalImage, new Size(544, 301));
                                        pictureBox1.Image = resizedImage;
                                        pictureBox1.Visible = true;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to load thumbnail: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Error checking link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Text = "Waiting link...";
                    }));
                }
            });

            textBox1.Enabled = true;
        }

        private string RemoveEmoji(string text)
        {
            Regex emojiRegex = new Regex(@"[\uD83C-\uDBFF\uDC00-\uDFFF\uD800-\uDFFF\u2600-\u27BF\uFE0F]", RegexOptions.Compiled);
            return emojiRegex.Replace(text, "");
        }

        private string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "_");
            }
            return fileName;
        }

        private async void DownloadVideo()
        {
            string defaultFileName = string.IsNullOrEmpty(videoTitle) ? "output" : videoTitle;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "All Files|*.*",
                FileName = defaultFileName
            };
            if (saveDialog.ShowDialog() != DialogResult.OK) return;
            savePath = saveDialog.FileName;

            progressBar1.Value = 0;
            progressBar1.BackColor = SystemColors.Control;
            isDownloading = true;
            button2.Enabled = false;
            button3.Enabled = false;
            textBox1.Enabled = false;
            button1.Text = "Cancel"; // 改為 Cancel

            await Task.Run(() =>
            {
                currentDownloadProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = $"-f bestvideo+bestaudio --no-playlist -o \"{savePath}.%(ext)s\" \"{currentUrl}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                currentDownloadProcess.OutputDataReceived += (s, ev) => UpdateProgress(ev.Data, "Downloading");
                currentDownloadProcess.ErrorDataReceived += (s, ev) => HandleError(ev.Data);

                try
                {
                    currentDownloadProcess.Start();
                    currentDownloadProcess.BeginOutputReadLine();
                    currentDownloadProcess.BeginErrorReadLine();
                    currentDownloadProcess.WaitForExit();

                    if (currentDownloadProcess.ExitCode == 0)
                    {
                        // 動態獲取實際下載檔案
                        string directory = Path.GetDirectoryName(savePath);
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(savePath);
                        string[] files = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*");
                        if (files.Length > 0)
                        {
                            savePath = files[0]; // 使用第一個匹配的檔案
                            Console.WriteLine($"Detected downloaded file: {savePath}"); // 調試訊息
                        }
                        else
                        {
                            throw new Exception("No downloaded file found.");
                        }

                        this.Invoke(new Action(() =>
                        {
                            if (Properties.Settings.Default.AutoConvert)
                            {
                                ConvertToMp4(savePath);
                            }
                            else
                            {
                                OnDownloadComplete();
                                ResetUIState();
                                button1.Text = "Confirm";
                            }
                        }));
                    }
                    else
                    {
                        throw new Exception("Download failed");
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Download error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 0;
                        progressBar1.BackColor = Color.Red;
                        ResetUIState();
                        button1.Text = "Confirm";
                    }));
                }
            });
        }

        private async void DownloadAudio()
        {
            string defaultFileName = string.IsNullOrEmpty(videoTitle) ? "output" : videoTitle;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "All Files|*.*",
                FileName = defaultFileName
            };
            if (saveDialog.ShowDialog() != DialogResult.OK) return;
            savePath = saveDialog.FileName;

            progressBar1.Value = 0;
            progressBar1.BackColor = SystemColors.Control;
            isDownloading = true;
            button2.Enabled = false;
            button3.Enabled = false;
            textBox1.Enabled = false;
            button1.Text = "Cancel"; // 改為 Cancel

            await Task.Run(() =>
            {
                currentDownloadProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = $"-f bestaudio --no-playlist -o \"{savePath}.%(ext)s\" \"{currentUrl}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                currentDownloadProcess.OutputDataReceived += (s, ev) => UpdateProgress(ev.Data, "Downloading");
                currentDownloadProcess.ErrorDataReceived += (s, ev) => HandleError(ev.Data);

                try
                {
                    currentDownloadProcess.Start();
                    currentDownloadProcess.BeginOutputReadLine();
                    currentDownloadProcess.BeginErrorReadLine();
                    currentDownloadProcess.WaitForExit();

                    if (currentDownloadProcess.ExitCode == 0)
                    {
                        // 動態獲取實際下載檔案
                        string directory = Path.GetDirectoryName(savePath);
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(savePath);
                        string[] files = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*");
                        if (files.Length > 0)
                        {
                            savePath = files[0]; // 使用第一個匹配的檔案
                            Console.WriteLine($"Detected downloaded file: {savePath}"); // 調試訊息
                        }
                        else
                        {
                            throw new Exception("No downloaded file found.");
                        }

                        this.Invoke(new Action(() =>
                        {
                            if (Properties.Settings.Default.AutoConvert)
                            {
                                ConvertToMp3(savePath);
                            }
                            else
                            {
                                OnDownloadComplete();
                                ResetUIState();
                                button1.Text = "Confirm";
                            }
                        }));
                    }
                    else
                    {
                        throw new Exception("Download failed");
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Download error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 0;
                        progressBar1.BackColor = Color.Red;
                        ResetUIState();
                        button1.Text = "Confirm";
                    }));
                }
            });
        }

        private void UpdateProgress(string data, string task)
        {
            if (string.IsNullOrEmpty(data)) return;
            Regex percentRegex = new Regex(@"(\d+\.\d+)%");
            var match = percentRegex.Match(data);
            if (match.Success)
            {
                if (float.TryParse(match.Groups[1].Value, out float percent))
                {
                    this.Invoke(new Action(() =>
                    {
                        Graphics g = progressBar1.CreateGraphics();
                        g.Clear(progressBar1.BackColor); // 清空畫布
                        progressBar1.Value = Math.Min((int)percent, 100);
                        g.DrawString($"{task}: {percent:F1}%", Font, Brushes.Black, new PointF(progressBar1.Width / 2 - 50, progressBar1.Height / 2 - 7));
                    }));
                }
            }
        }

        private void HandleError(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                this.Invoke(new Action(() => MessageBox.Show(data, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        private async void ConvertToMp4(string filePath)
        {
            string ffmpegPath = FindFmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("ffmpeg not found in PATH or C:\\ffmpeg\\bin", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    progressBar1.Value = 0;
                    progressBar1.BackColor = Color.Red;
                    ResetUIState();
                    button1.Text = "Confirm";
                }));
                return;
            }

            string outputPath = Path.ChangeExtension(filePath, ".mp4");

            await Task.Run(() =>
            {
                currentConvertProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{filePath}\" -c:v copy -c:a aac \"{outputPath}\"",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                currentConvertProcess.ErrorDataReceived += (s, ev) => UpdateProgress(ev.Data, "Converting");

                try
                {
                    currentConvertProcess.Start();
                    currentConvertProcess.BeginErrorReadLine();
                    currentConvertProcess.WaitForExit();

                    if (currentConvertProcess.ExitCode == 0)
                    {
                        File.Delete(filePath);
                        savePath = outputPath;
                        this.Invoke(new Action(() =>
                        {
                            OnDownloadComplete();
                            ResetUIState();
                            button1.Text = "Confirm";
                        }));
                    }
                    else
                    {
                        throw new Exception("Conversion failed with exit code: " + currentConvertProcess.ExitCode);
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Conversion error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 0;
                        progressBar1.BackColor = Color.Red;
                        ResetUIState();
                        button1.Text = "Confirm";
                    }));
                }
            });
        }

        private async void ConvertToMp3(string filePath)
        {
            string ffmpegPath = FindFmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("ffmpeg not found in PATH or C:\\ffmpeg\\bin", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    progressBar1.Value = 0;
                    progressBar1.BackColor = Color.Red;
                    ResetUIState();
                    button1.Text = "Confirm";
                }));
                return;
            }

            string outputPath = Path.ChangeExtension(filePath, ".mp3");

            await Task.Run(() =>
            {
                currentConvertProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{filePath}\" -c:a mp3 \"{outputPath}\"",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                currentConvertProcess.ErrorDataReceived += (s, ev) => UpdateProgress(ev.Data, "Converting");

                try
                {
                    currentConvertProcess.Start();
                    currentConvertProcess.BeginErrorReadLine();
                    currentConvertProcess.WaitForExit();

                    if (currentConvertProcess.ExitCode == 0)
                    {
                        File.Delete(filePath);
                        savePath = outputPath;
                        this.Invoke(new Action(() =>
                        {
                            OnDownloadComplete();
                            ResetUIState();
                            button1.Text = "Confirm";
                        }));
                    }
                    else
                    {
                        throw new Exception("Conversion failed with exit code: " + currentConvertProcess.ExitCode);
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show($"Conversion error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressBar1.Value = 0;
                        progressBar1.BackColor = Color.Red;
                        ResetUIState();
                        button1.Text = "Confirm";
                    }));
                }
            });
        }

        private void ResetUIState()
        {
            isDownloading = false;
            button2.Enabled = true;
            button3.Enabled = true;
            textBox1.Enabled = true;
        }

        private string FindFmpegPath()
        {
            string specificPath = @"C:\ffmpeg\bin\ffmpeg.exe";
            if (File.Exists(specificPath))
            {
                return specificPath;
            }

            string path = Environment.GetEnvironmentVariable("PATH");
            string[] directories = path.Split(';');
            foreach (string dir in directories)
            {
                string ffmpeg = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(ffmpeg))
                {
                    return ffmpeg;
                }
            }
            return null;
        }

        private void OnDownloadComplete()
        {
            // 添加調試訊息
            Console.WriteLine($"OnDownloadComplete triggered at {DateTime.Now}. DownloadSound: {Properties.Settings.Default.DownloadSound}, DownloadNotify: {Properties.Settings.Default.DownloadNotify}");

            if (Properties.Settings.Default.DownloadSound)
            {
                try
                {
                    using (System.Media.SoundPlayer player = new System.Media.SoundPlayer(Properties.Resources.Notify))
                    {
                        player.Play();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to play sound: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (Properties.Settings.Default.DownloadNotify)
            {
                try
                {
                    NotifyIcon notify = new NotifyIcon
                    {
                        BalloonTipTitle = "Download Complete",
                        BalloonTipText = $"{Path.GetFileName(savePath)} is done!",
                        Icon = SystemIcons.Information,
                        Visible = true
                    };
                    notify.ShowBalloonTip(5000); // 延長顯示時間至 5 秒
                    notify.BalloonTipClicked += (s, ev) => Process.Start("explorer.exe", $"/select,\"{savePath}\"");
                    notify.Dispose(); // 釋放資源
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to show notification: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentUrl))
            {
                MessageBox.Show("Please confirm a valid link first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DownloadVideo();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentUrl))
            {
                MessageBox.Show("Please confirm a valid link first", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DownloadAudio();
        }
    }
}