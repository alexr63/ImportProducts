using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Objects;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ImportProducts
{
    public partial class Form1 : Form
    {
        private ImportProductsEntities context;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            BindData();
        }

        private void BindData()
        {
            context = new ImportProductsEntities();
            var query = context.Feeds;
            dataGridView1.DataSource = query.ToList();
        }

        private void toolStripMenuItemRun_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow selectedRow in dataGridView1.SelectedRows)
            {
                var selectedFeed = selectedRow.DataBoundItem as Feed;

                // hourglass cursor
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    context = new ImportProductsEntities();
                    Feed feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                    string message = String.Empty;
                    bool rc = false;
                    switch (selectedFeed.Name)
                    {
                        case "Hotels":
                            rc = ImportHotels.DoImport(selectedFeed.URL, out message);
                            break;
                        case "Trade Doubler":
                            rc = ImportTradeDoublerProducts.DoImport(selectedFeed.URL, out message);
                            break;
                    }
                    if (rc)
                    {
                        feed.LastRun = DateTime.Now;
                        feed.Status = "Success";
                        statusStrip1.Items[0].Text = String.Format("{0} imported successfully at {1}",
                                                                   selectedFeed.Name, DateTime.Now);
                    }
                    else
                    {
                        feed.LastRun = DateTime.Now;
                        feed.Status = "Error";
                        statusStrip1.Items[0].Text = String.Format("{0} not imported. Error message: {1}",
                                                                   selectedFeed.Name, message);
                    }
                    context.SaveChanges();
                    BindData();
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
        }

        private void toolStripMenuItemProperties_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
            var selectedFeed = selectedRow.DataBoundItem as Feed;
            EditProperties editProperties = new EditProperties();
            editProperties.labelName.Text = selectedFeed.Name;
            editProperties.textBoxURL.Text = selectedFeed.URL;
            if (selectedFeed.LastRun != null)
            {
                editProperties.labelLastRun.Text = selectedFeed.LastRun.Value.ToString();
            }
            if (selectedFeed.Status != null)
            {
                editProperties.labelStatus.Text = selectedFeed.Status;
            }
            if (editProperties.ShowDialog(this) == DialogResult.OK)
            {
                context = new ImportProductsEntities();
                Feed feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                feed.URL = editProperties.textBoxURL.Text;
                context.SaveChanges();
                BindData();
            }
            editProperties.Dispose();
        }

        private void toolStripMenuItemFeed_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItemRun.Enabled = dataGridView1.SelectedRows.Count > 0;
            toolStripMenuItemProperties.Enabled = dataGridView1.SelectedRows.Count == 1;
        }
    }
}
