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

    // list of parameters that background operation should get
    struct BackgroundWorkParameters
    {
        public string Url;
        public string Category;
        public int PortalId;
    }

    public partial class Form1 : Form
    {
        private ImportProductsEntities context;
        
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
            context = new ImportProductsEntities();
            var query = context.Feeds;
            dataGridView1.DataSource = query.ToList();
        }

        private void toolStripMenuItemRun_Click(object sender, EventArgs e)
        {
            string keyDownload;
            foreach (DataGridViewRow selectedRow in dataGridView1.SelectedRows)
            {
               var selectedFeed = selectedRow.DataBoundItem as Feed;
               keyDownload = selectedFeed.Name; 
                if (!bgw.Keys.Contains(keyDownload))
                {
                    context = new ImportProductsEntities();
                    feed = context.Feeds.SingleOrDefault(f => f.Id == selectedFeed.Id);
                    bgProcesses.Add(keyDownload, feed);
                    // set parameters for background process here if lists of parameters are the same for different rows in Feeds
                    // if it is necessary send different parameter`s list to background operation, assign parameters in block 
                    // 'switch' as well as set 'BackGroundWorkerDelegateWork' for each row in Feeds
                    BackgroundWorkParameters bgParams = new BackgroundWorkParameters();
                    bgParams.Url = selectedFeed.URL;
                    bgParams.Category = "";   // selectedFeed.Category for instance or something like that
                    bgParams.PortalId = 0;   // selectedFeed.PortalId for instance or something like that

                    switch (keyDownload)
                    {
                        case "Hotels":
                            workD = new BackGroundWorkerDelegateWork(ImportHotels.DoImport);
                            break;
                        case "Trade Doubler":
                            workD = new BackGroundWorkerDelegateWork(ImportTradeDoublerProducts.DoImport);
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
            if (activeKey != "" && bw.Equals(bgw[activeKey])) displayedProcess = true;  // finish displayed process
            // finish showed process
            bgProcesses[activeKey].LastRun = DateTime.Now;
            if (e.Cancelled)
            {
                deactivate = true;
                bgProcesses[activeKey].Status = "Cancel";
                notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                notifyIcon.BalloonTipText = "Import " + activeKey + " has been canceled";
            }
            else
                if (e.Error != null || e.Result.ToString().Substring(0, 6).Equals("ERROR:"))
                {
                    deactivate = true;
                    bgProcesses[activeKey].Status = "Error";
                    notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
                    notifyIcon.BalloonTipText = "Import " + activeKey + " has been broken due to ERROR";
                }
                else
                {
                    deactivate = true;
                    bgProcesses[activeKey].Status = "Success";
                    notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                    notifyIcon.BalloonTipText = "Import " + activeKey + " completed";
                }
            // ShowBaloon for non-active process
            notifyIcon.ShowBalloonTip(15000);
            if (displayedProcess && deactivate)
            {
                if (bw.WorkerReportsProgress)
                {   // disable controls for displayed process
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
                    ShiftStatusStripToActiveBackgroudWorker(bgw.Count, activeKey, bgStep[activeKey], bgw[activeKey].IsBusy, bgProgress[activeKey]);
                else
                    ShiftStatusStripToActiveBackgroudWorker(0, activeKey, activeKey, false, 0);
            }
            // set result of download
            context.SaveChanges();
            BindData();
        }

 

        private void cancelBGW(object sender, EventArgs e)
        {
            //                bgw[activeKey].IsBusy &&

            if (activeKey != "" &&
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

    }
}
