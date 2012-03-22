using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ImportProducts
{
    public partial class EditProperties : Form
    {
        public EditProperties()
        {
            InitializeComponent();
        }

        private void textBoxURL_Validating(object sender, CancelEventArgs e)
        {
            if (textBoxURL.Text.Length > 0)
            {
                errorProvider1.SetError(textBoxURL, "");
            }
            else
            {
                errorProvider1.SetError(textBoxURL, "Please enter URL");
            }
        }

        private void textBoxCategory_Validating(object sender, CancelEventArgs e)
        {
            if (textBoxCategory.Text.Length > 0)
            {
                errorProvider1.SetError(textBoxCategory, "");
            }
            else
            {
                errorProvider1.SetError(textBoxCategory, "Please enter Category");
            }
        }

        private void textBoxAdvancedCategoryRoot_Validating(object sender, CancelEventArgs e)
        {
            if (textBoxAdvancedCategoryRoot.Text.Length > 0)
            {
                errorProvider1.SetError(textBoxAdvancedCategoryRoot, "");
            }
            else
            {
                errorProvider1.SetError(textBoxAdvancedCategoryRoot, "Please enter Advanced Category Root");
            }
        }
    }
}
