using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pien_herkun_softa
{
    public partial class Form1 : Form
    {
        int clickCount = 0;
        bool epic = false;
        string epictext;
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            clickCount++;

            if (epic == false)
            {
                epictext = "eaoighapog";
            }
            if (epic == true)
            {
                epictext = "oookooo jeee";
            }

            MessageBox.Show($"Hello World! You clicked {clickCount} times. and the boolean is { epictext }");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false)
            {
                epic = false;
            }
            if (checkBox1.Checked == true)
            {
                epic = true;
            }
        }
    }
}
