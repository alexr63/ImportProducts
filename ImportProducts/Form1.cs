using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace ImportProducts
{

    // list of parameters that background operation should get
    struct BackgroundWorkParameters
    {
        public string Url;
        public int CategoryId;
        public int PortalId;
        public int VendorId;
        public string AdvancedCategoryRoot;
        public string CountryFilter;
        public string CityFilter;
        public int? StepImport;
        public int? StepAddToCategories;
        public int? StepAddImages;
    }

    public partial class Form1 : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private SelectedHotelsEntities context;
        
        private Feed feed;

        #region Orhanization backgroundwork 
        // Event handlers for backgroundWorker events
        delegate void BackGroundWorkerDelegateWork(object sender, DoWorkEventArgs e);
        delegate void BackGroundWorkerDelegateProgress(object sender, ProgressChangedEventArgs e);
        delegate void BackGroundWorkerDelegateCompleted(object sender, RunWorkerCompletedEventArgs e);

        Dictionary<string, BackgroundWorker> bgw = new Dictionary<string, BackgroundWorker>();      // List BackgroundWorker for different downloads
        Dictionary<string, Feed> bgProcesses = new Dictionary<string, Feed>();                  // aliases&feed of background operations
        Dictionary<string, string> bgStep = new Dictionary<string, string>();                  // aliases&feed of background operations
        Dictionary<string, int> bgProgress = new Dictionary<string, int>();                  // aliases&feed of background operations
        string activeKey = "";                                                     // Id active download which shown by StatusStrip
        public static string activeStep = "";                                                       // current download step 
        //string errMessage = "";                                                        // describe error which happens during download
        BackGroundWorkerDelegateWork workD;
        BackGroundWorkerDelegateProgress progressD;
        BackGroundWorkerDelegateCompleted completeD;
        // Create new BackgroundWorker
        private BackgroundWorker AddBackGroundWorker(BackGroundWorkerDelegateWork bgwDoWork,
                                                     BackGroundWorkerDelegateProgress bgwProgressChanged,
                                                     BackGroundWorkerDelegateCompleted bgwRunWorkerCompleted)
        {
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += new DoWorkEventHandler(bgwDoWork);
            backgroundWorker.ProgressChanged += new ProgressChangedEventHandler(bgwProgressChanged);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgwRunWorkerCompleted);
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            return backgroundWorker;
        }
        #endregion

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
            context = new SelectedHotelsEntities();
            var query = context.Feeds;
            dataGridView1.DataSource = query.ToList();
        }

        private void toolStripMenuItemRun_Click(object sender, EventArgs e)
        {
            StartWorkerProcess();
        }

        private void StartWorkerProcess(int? stepImport = null, int? stepAddToCategories = null, int? stepAddImages = null)
        {
            string keyDownload;
            foreach (DataGridViewRow selectedRow in dataGridView1.SelectedRows)
            {
                var selectedFeed = selectedRow.DataBoundItem as Feed;
                keyDownload = selectedFeed.Name; 
                if (!bgw.Keys.Contains(keyDownload))
                {
                    context = new SelectedHotelsEntities();
                    feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                    bgProcesses.Add(keyDownload, feed);
                    // set parameters for background process here if lists of parameters are the same for different rows in Feeds
                    // if it is necessary send different parameter`s list to background operation, assign parameters in block 
                    // 'switch' as well as set 'BackGroundWorkerDelegateWork' for each row in Feeds
                    BackgroundWorkParameters bgParams = new BackgroundWorkParameters();
                    bgParams.Url = selectedFeed.URL;
                    if (!String.IsNullOrEmpty(selectedFeed.Category))
                    {
                        using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                        {
                            var category = db.Categories.SingleOrDefault(c => c.Name == selectedFeed.Category);
                            if (category != null)
                            {
                                bgParams.CategoryId = category.Id;
                            }
                            else
                            {
                                category = new Category();
                                category.Name = selectedFeed.Category;
                                db.Categories.Add(category);
                                db.SaveChanges();
                                bgParams.CategoryId = category.Id;
                            }
                        }
                    }
                    bgParams.PortalId = selectedFeed.PortalId;
                    bgParams.VendorId = selectedFeed.VendorId;
                    bgParams.AdvancedCategoryRoot = selectedFeed.AdvancedCategoryRoot;
                    bgParams.CountryFilter = selectedFeed.CountryFilter;
                    bgParams.CityFilter = selectedFeed.CityFilter;
                    bgParams.StepImport = stepImport;
                    bgParams.StepAddToCategories = stepAddToCategories;
                    bgParams.StepAddImages = stepAddImages;

                    switch (keyDownload)
                    {
                        case "Laterooms":
                        case "Laterooms (filtered)":
                            workD = new BackGroundWorkerDelegateWork(ImportLaterooms.DoImport);
                            break;
                        case "Trade Doubler":
                        case "Home and garden":
                        case "Clothes":
                            workD = new BackGroundWorkerDelegateWork(ImportTradeDoublerProducts.DoImport);
                            break;
                        case "Trade Doubler Hotels":
                            workD = new BackGroundWorkerDelegateWork(ImportTradeDoublerHotels.DoImport);
                            break;
                        case "Productserve":
#if ImportProductserve
                            workD = new BackGroundWorkerDelegateWork(ImportProductserve.DoImport);
#endif
                            break;
                        case "Webgains":
                            workD = new BackGroundWorkerDelegateWork(ImportWebgainsProducts.DoImport);
                            break;
                    }
                    progressD = new BackGroundWorkerDelegateProgress(backgroundWorkerProgressChanged);
                    completeD = new BackGroundWorkerDelegateCompleted(backgroundWorkerRunWorkerCompleted);
                    bgw.Add(keyDownload, AddBackGroundWorker(workD, progressD, completeD));
                    bgProgress.Add(keyDownload, 0);
                    activeStep = "Start download";
                    bgStep.Add(keyDownload, activeStep);
                    bgw[keyDownload].RunWorkerAsync(bgParams);      // selectedFeed.URL
                }
                activeKey = keyDownload;
                // display status
                ShiftStatusStripToActiveBackgroudWorker(bgw.Count, activeKey, activeStep, bgw[activeKey].IsBusy, bgProgress[activeKey]);
            }
        }

        private void toolStripMenuItemProperties_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
            var selectedFeed = selectedRow.DataBoundItem as Feed;
            EditProperties editProperties = new EditProperties();
            editProperties.labelName.Text = selectedFeed.Name;
            editProperties.textBoxURL.Text = selectedFeed.URL;
            editProperties.comboBoxCategory.Text = selectedFeed.Category;
            editProperties.numericUpDownPortalId.Value = selectedFeed.PortalId;
            editProperties.numericUpDownVendorId.Value = selectedFeed.VendorId;
            editProperties.textBoxAdvancedCategoryRoot.Text = selectedFeed.AdvancedCategoryRoot;
            editProperties.comboBoxCountry.Text = selectedFeed.CountryFilter;
            editProperties.textBoxCity.Text = selectedFeed.CityFilter;
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
                context = new SelectedHotelsEntities();
                Feed feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                feed.URL = editProperties.textBoxURL.Text;
                feed.Category = editProperties.comboBoxCategory.Text;
                feed.PortalId = (int)editProperties.numericUpDownPortalId.Value;
                feed.VendorId = (int)editProperties.numericUpDownVendorId.Value;
                feed.AdvancedCategoryRoot = editProperties.textBoxAdvancedCategoryRoot.Text;
                feed.CountryFilter = editProperties.comboBoxCountry.Text;
                feed.CityFilter = editProperties.textBoxCity.Text;
                feed.StepImport = null;
                feed.StepAddToCategories = null;
                feed.StepAddImages = null;
                context.SaveChanges();
                BindData();
            }
            editProperties.Dispose();
        }

        private void toolStripMenuItemFeed_DropDownOpening(object sender, EventArgs e)
        {
            toolStripMenuItemRun.Enabled = dataGridView1.SelectedRows.Count > 0;
            toolStripMenuItemProperties.Enabled = dataGridView1.SelectedRows.Count == 1;
            toolStripDeleteProducts.Enabled = dataGridView1.SelectedRows.Count == 1;

            DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
            var selectedFeed = selectedRow.DataBoundItem as Feed;
            context = new SelectedHotelsEntities();
            feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
            if (feed.Name == "Laterooms" && (feed.StepImport != null || feed.StepAddToCategories != null || feed.StepAddImages != null))
            {
                toolStripMenuItemResume.Enabled = dataGridView1.SelectedRows.Count == 1;
            }
            else
            {
                toolStripMenuItemResume.Enabled = false;
            }

            toolStripMenuItemCopy.Enabled = dataGridView1.SelectedRows.Count == 1;
            toolStripMenuItemDelete.Enabled = dataGridView1.SelectedRows.Count == 1;
        }

        // SergePSV - check unfinished downloads
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Do you want to close application?", "Exit application", MessageBoxButtons.YesNo) == DialogResult.No) e.Cancel = true;
            else
            {
                int countUnfinishedProcess = 0;
                foreach (BackgroundWorker bw in bgw.Values)
                    if (bw.IsBusy) countUnfinishedProcess++;
                if (countUnfinishedProcess > 0)
                    if (MessageBox.Show("There are " + countUnfinishedProcess.ToString() + " active background operations. If you closing the application some data will be lost.\nYou do want to close application?", "Cancel operations", MessageBoxButtons.YesNo) == DialogResult.No) e.Cancel = true;
                    else
                    {
                        foreach (BackgroundWorker bw in bgw.Values)
                            if (bw.IsBusy) bw.CancelAsync();
                        // likely here should be code that check when all active background downloads will be cancelled and after that close application 

                        //
                        MessageBox.Show("All background operations were canceled");
                    }
            }
        }
        // ProgressBar for background
        private void backgroundWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            if (activeKey != "" && bw.Equals(bgw[activeKey]) && bw.WorkerReportsProgress)
            {   // displayed process
                if (e.ProgressPercentage == 0) SetInfoForNewStep(activeStep);      // for new process display step
                // update active step
                bgStep[activeKey] = activeStep;
                SetInfoForNewStep(activeStep);
                tsProgressBar.Value = (e.ProgressPercentage > 100) ? 100 : e.ProgressPercentage;
                bgProgress[activeKey] = tsProgressBar.Value;
             }
            else
            {
                bgProgress[activeKey] = bgProgress[activeKey];
            }
        }

        private void backgroundWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool displayedProcess = false, deactivate = false;
            BackgroundWorker bw = sender as BackgroundWorker;
            if (activeKey != "" && bw.Equals(bgw[activeKey])) displayedProcess = true; // finish displayed process
            // finish showed process
            bgProcesses[activeKey].LastRun = DateTime.Now;
            string ballonTextFirst = (activeKey.Substring(0, 6).Equals("DELETE") ? "" : "Import ") + activeKey;
            if (e.Cancelled)
            {
                deactivate = true;
                bgProcesses[activeKey].Status = "Cancel";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                notifyIcon.BalloonTipText = ballonTextFirst + " has been canceled";
            }
            else if (e.Error != null || (e.Result != null && e.Result.ToString().Substring(0, 6).Equals("ERROR:")))
            {
                deactivate = true;
                bgProcesses[activeKey].Status = "Error";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                notifyIcon.BalloonTipText = ballonTextFirst + " has been broken due to ERROR";
            }
            else
            {
                deactivate = true;
                bgProcesses[activeKey].Status = "Success";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon.BalloonTipText = ballonTextFirst + " completed";
            }
            // ShowBaloon for non-active process
            notifyIcon.ShowBalloonTip(15000);
            if (displayedProcess && deactivate)
            {
                if (bw.WorkerReportsProgress)
                {
                    // disable controls for displayed process
                    tsProgressBar.Enabled = false;
                    tsddButton.Enabled = false;
                }
                // remove backgroundworker & alias of downloads
                bgw.Remove(activeKey);
                bgProcesses.Remove(activeKey);
                bgStep.Remove(activeKey);
                bgProgress.Remove(activeKey);
                // find another active process for displaying
                activeKey = "";
                foreach (string key in bgw.Keys)
                    if (bgw[key].IsBusy)
                    {
                        activeKey = key;
                        break;
                    }
                if (activeKey != "")
                    ShiftStatusStripToActiveBackgroudWorker(bgw.Count, activeKey, bgStep[activeKey],
                                                            bgw[activeKey].IsBusy, bgProgress[activeKey]);
                else
                    ShiftStatusStripToActiveBackgroudWorker(0, activeKey, activeKey, false, 0);
            }
            // set result of download
            try
            {
                context.SaveChanges();
            }
            catch (DbEntityValidationException dbEx)
            {
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Trace.TraceInformation("Property: {0} Error: {1}", validationError.PropertyName,
                                               validationError.ErrorMessage);
                    }
                }
            }

            BindData();
        }

 

        private void cancelBGW(object sender, EventArgs e)
        {
            //                bgw[activeKey].IsBusy &&

            if (activeKey != "" && !activeKey.Substring(0,6).Equals("DELETE") &&            // cancellation of deleting is prohibited
                bgw[activeKey].WorkerSupportsCancellation &&
                MessageBox.Show("Do you really want to cancel?", "Cancelling background process", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                bgw[activeKey].CancelAsync();

            //    //bgw[activeKey].CancelAsync();       // send request for cancelling operation
            //else
            //    if (activeKey != "" )   // remove info about error operation  &&                     !bgw[activeKey].IsBusy
            //    {
            //        // remove backgroundworker & alias of downloads
            //        bgw.Remove(activeKey);
            //        bgProcesses.Remove(activeKey);
            //        bgStep.Remove(activeKey);
            //        bgProgress.Remove(activeKey);
            //        // find another active process for displaying
            //        activeKey = "";
            //        foreach (string key in bgw.Keys)
            //            if (bgw[key].IsBusy)
            //            {
            //                activeKey = key;
            //                break;
            //            }
            //        if (activeKey != "")
            //            ShiftStatusStripToActiveBackgroudWorker(bgw.Count, activeKey, bgStep[activeKey], bgw[activeKey].IsBusy, bgProgress[activeKey]);
            //        else
            //            ShiftStatusStripToActiveBackgroudWorker(0, activeKey, activeKey, false, 0);
            //    }
        }

        private void ShiftStatusStripToActiveBackgroudWorker(int count, string current, string info, bool enableCancelButton,  int valueProgressBar)
        {
            tsslTotalProcess.Text = "Total active process: " + count.ToString().PadLeft(2, ' ');
            tsslCurrent.Text = current;
            tsslInfo.Text = info;
            tsddButton.Enabled = enableCancelButton; // 
            tsProgressBar.Value = valueProgressBar; //  
            //tsProgressBar.Invalidate();
            //statusStrip.Refresh();
        }
        // set ext info about process - its step
        private void SetInfoForNewStep(string info)
        {
            tsslInfo.Text = info;
            //statusStrip.Refresh();
        }
        // display status background process if it exists
        private void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                var selectedFeed = dataGridView1.Rows[e.RowIndex].DataBoundItem as Feed;
                if (bgw.Keys.Contains(selectedFeed.Name))  // exist bgProcess for this row
                {
                    activeKey = selectedFeed.Name;
                    ShiftStatusStripToActiveBackgroudWorker(bgw.Count, selectedFeed.Name, bgStep[selectedFeed.Name], bgw[selectedFeed.Name].IsBusy, bgProgress[selectedFeed.Name]);
                }
            }
        }

            // Show the form when the user clicks on the notify icon.
        private void notifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            // Set the WindowState to normal if the form is minimized.
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            // Activate the form.
            this.Activate();
        }

        private void toolStripDeleteProducts_Click(object sender, EventArgs e)
        {
            // background DELETING is locked for cancel and if during deleting user shift "ProgressBar" to another bg operation it is impossible to return to deleting progressbar.
            // after finish DELETING BalloonTip will be displayed
            try
            {
                DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
                var selectedFeed = selectedRow.DataBoundItem as Feed;
                var vendorId = selectedFeed.VendorId;
                string keyDownload = "DELETE_" + selectedFeed.Name;    
                if (!bgw.Keys.Contains(keyDownload))
                {
                    context = new SelectedHotelsEntities();
                    feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                    bgProcesses.Add(keyDownload, feed);
                    workD = new BackGroundWorkerDelegateWork(DeleteProducts);
                    progressD = new BackGroundWorkerDelegateProgress(backgroundWorkerProgressChanged);
                    completeD = new BackGroundWorkerDelegateCompleted(backgroundWorkerRunWorkerCompleted);
                    bgw.Add(keyDownload, AddBackGroundWorker(workD, progressD, completeD));
                    bgProgress.Add(keyDownload, 0);
                    activeStep = "Deleting products...";
                    bgStep.Add(keyDownload, activeStep);
                    bgw[keyDownload].RunWorkerAsync(vendorId);     
                }
                activeKey = keyDownload;
                // display status
                ShiftStatusStripToActiveBackgroudWorker(bgw.Count, activeKey, activeStep, bgw[activeKey].IsBusy, bgProgress[activeKey]);
            }
            catch (Exception exception)
            {
                activeStep = exception.Message;
            }
        }

        private void DeleteProducts(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            int vendorId = (int)e.Argument;

            using (SelectedHotelsEntities db = new SelectedHotelsEntities())
            {
                bw.ReportProgress(0);           // start new step of background process

                using (var context = new SelectedHotelsEntities())
                {
                    Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                    feed.StepImport = null;
                    feed.StepAddToCategories = null;
                    feed.StepAddImages = null;
                    context.SaveChanges();
                }

                try
                {
#if LINQDELETION
                    foreach (var vendorProduct in vendorProducts.ToList())
                    {
                        db.Products.Remove(vendorProduct);

                        currentDeletedProduct++;
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        else if (bw.WorkerReportsProgress && currentDeletedProduct % 100 == 0) bw.ReportProgress((int)(100 * currentDeletedProduct / countDeletedProducts));
                    }
                    db.SaveChanges();
#else
                    using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                    {
                        destinationConnection.Open();
                        while (db.Products.Count(p => p.CreatedByUser == vendorId) > 0)
                        {
                            SqlCommand commandDelete =
                                new SqlCommand(
                                    "delete top(500) from CAT_Products where CreatedByUser = @CreatedByUser",
                                    destinationConnection);
                            commandDelete.Parameters.Add("@CreatedByUser", SqlDbType.Int);
                            commandDelete.Parameters["@CreatedByUser"].Value = vendorId;
                            commandDelete.ExecuteNonQuery();
                        }
                    }
#endif
                }
                catch (Exception ex)
                {
                    e.Result = "ERROR:" + ex.Message;
                    log.Error("Error error logging", ex);
                }
            }
        }

        private void toolStripMenuItemResume_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
            var selectedFeed = selectedRow.DataBoundItem as Feed;
            context = new SelectedHotelsEntities();
            feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
            StartWorkerProcess(feed.StepImport, feed.StepAddToCategories, feed.StepAddImages);
        }

        private void toolStripMenuItemCopy_Click(object sender, EventArgs e)
        {
            DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
            var selectedFeed = selectedRow.DataBoundItem as Feed;
            var newFeed = new Feed();
            newFeed.Name = selectedFeed.Name + " - Copy";
            newFeed.Description = selectedFeed.Description;
            newFeed.URL = selectedFeed.URL;
            newFeed.PortalId = selectedFeed.PortalId;
            newFeed.Category = selectedFeed.Category;
            newFeed.VendorId = selectedFeed.VendorId;
            newFeed.AdvancedCategoryRoot = selectedFeed.AdvancedCategoryRoot;
            using (SelectedHotelsEntities db = new SelectedHotelsEntities())
            {
                db.Feeds.Add(newFeed);
                db.SaveChanges();
            }
            BindData();
        }

        private void toolStripMenuItemDelete_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete this item?", "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                DataGridViewRow selectedRow = dataGridView1.SelectedRows[0];
                var selectedFeed = selectedRow.DataBoundItem as Feed;
                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    var feed = db.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                    if (feed != null)
                    {
                        db.Feeds.Remove(feed);
                        db.SaveChanges();
                    }
                }
                BindData();
            }
        }

        private void deleteLocationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SelectedHotelsEntities db = new SelectedHotelsEntities())
            {
                Common.UpdateLocationLeveling(db);
                Common.DeleteEmptyLocations(db);
            }
        }
    }
}
