using System;
using System.Net;
using System.Windows.Forms;

namespace EzYTDlp_V2
{
    public partial class Launcher : Form
    {
        public Launcher()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Hide();
            new Main().ShowDialog();
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Hide();
            new Settings().ShowDialog();
            this.Close();
        }
    }
}