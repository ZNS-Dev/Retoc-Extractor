using System;
using System.Windows.Forms;

namespace RetocExtractor
{
    public partial class ProfileNameDialog : Form
    {
        public ProfileNameDialog()
        {
            InitializeComponent();
        }

        public string ProfileName
        {
            get { return (TxtProfileName.Text ?? string.Empty).Trim(); }
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                lblValidation.Text = "Enter a profile name.";
                TxtProfileName.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void TxtProfileName_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ProfileName))
                lblValidation.Text = string.Empty;
        }
    }
}
