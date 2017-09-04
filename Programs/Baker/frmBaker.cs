using System;
using System.DrawingCore;
using System.Windows.Forms;
using System.IO;
using OpenMetaverse.Imaging;

namespace Baker
{
    public partial class FrmBaker : Form
    {
        Bitmap _alphaMask;

        public FrmBaker()
        {
            InitializeComponent();
        }

        private void frmBaker_Load(object sender, EventArgs e)
        {
            cboMask.SelectedIndex = 0;
            DisplayResource(cboMask.Text);
        }

        private void DisplayResource(string resource)
        {
            Stream stream = OpenMetaverse.Helpers.GetResourceStream(resource + ".tga");

            if (stream != null)
            {
                _alphaMask = LoadTGAClass.LoadTGA(stream);
                stream.Close();

                //ManagedImage managedImage = new ManagedImage(AlphaMask);

                // FIXME: Operate on ManagedImage instead of Bitmap
                pic1.Image = (System.Drawing.Bitmap)(object)Oven.ModifyAlphaMask(_alphaMask, (byte)scrollWeight.Value, 0.0f); // *HACK:
            }
            else
            {
                MessageBox.Show("Failed to load embedded resource \"" + resource + "\"", "Baker",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void scrollWeight_Scroll(object sender, ScrollEventArgs e)
        {
            pic1.Image = (System.Drawing.Bitmap)(object)Oven.ModifyAlphaMask(_alphaMask, (byte)scrollWeight.Value, 0.0f); // *HACK:
        }

        private void frmBaker_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void cmdLoadSkin_Click(object sender, EventArgs e)
        {

        }

        private void cboMask_SelectedIndexChanged(object sender, EventArgs e)
        {
            DisplayResource(cboMask.Text);
        }
    }
}
