using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SelectedHotelsModel;

namespace ImportProducts
{
    public partial class EditProperties : Form
    {
        public EditProperties()
        {
            InitializeComponent();
            SelectedHotelsEntities db = new SelectedHotelsEntities();
            List<Location> countries = (from l in db.Locations
                                        where l.ParentId == null
                                       orderby l.Name
                                       select l).ToList();
            Location emptyCountry = new Location { Id = 0, Name = String.Empty };
            countries.Insert(0, emptyCountry);
            comboBoxCountry.DataSource = countries;

            List<ProductType> productTypes = (from t in db.ProductTypes
                                              orderby t.Name
                                              select t).ToList();
            ProductType emptyProductType = new ProductType {Id = 0, Name = "please select"};
            productTypes.Insert(0, emptyProductType);
            comboBoxCategory.DataSource = productTypes;
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

        private void comboBoxCategory_Validating(object sender, CancelEventArgs e)
        {
            if (comboBoxCategory.SelectedIndex != 0)
            {
                errorProvider1.SetError(comboBoxCategory, "");
            }
            else
            {
                errorProvider1.SetError(comboBoxCategory, "Please select Category");
            }
        }
    }
}
