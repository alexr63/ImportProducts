namespace ImportProducts
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItemFeed = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemProperties = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemRun = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripDeleteProducts = new System.Windows.Forms.ToolStripMenuItem();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.tsslTotalProcess = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.tsddButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.cancelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tsslCurrent = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsslInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemFeed});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(763, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItemFeed
            // 
            this.toolStripMenuItemFeed.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemProperties,
            this.toolStripMenuItemRun,
            this.toolStripDeleteProducts});
            this.toolStripMenuItemFeed.Name = "toolStripMenuItemFeed";
            this.toolStripMenuItemFeed.Size = new System.Drawing.Size(44, 20);
            this.toolStripMenuItemFeed.Text = "Feed";
            this.toolStripMenuItemFeed.DropDownOpening += new System.EventHandler(this.toolStripMenuItemFeed_DropDownOpening);
            // 
            // toolStripMenuItemProperties
            // 
            this.toolStripMenuItemProperties.Name = "toolStripMenuItemProperties";
            this.toolStripMenuItemProperties.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemProperties.Text = "Properties...";
            this.toolStripMenuItemProperties.Click += new System.EventHandler(this.toolStripMenuItemProperties_Click);
            // 
            // toolStripMenuItemRun
            // 
            this.toolStripMenuItemRun.Name = "toolStripMenuItemRun";
            this.toolStripMenuItemRun.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemRun.Text = "Run";
            this.toolStripMenuItemRun.Click += new System.EventHandler(this.toolStripMenuItemRun_Click);
            // 
            // toolStripDeleteProducts
            // 
            this.toolStripDeleteProducts.Name = "toolStripDeleteProducts";
            this.toolStripDeleteProducts.Size = new System.Drawing.Size(157, 22);
            this.toolStripDeleteProducts.Text = "Delete Products";
            this.toolStripDeleteProducts.Click += new System.EventHandler(this.toolStripDeleteProducts_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 24);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(763, 238);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.RowEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_RowEnter);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsslTotalProcess,
            this.tsProgressBar,
            this.tsddButton,
            this.tsslCurrent,
            this.tsslInfo});
            this.statusStrip.Location = new System.Drawing.Point(0, 240);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(763, 22);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "statusStrip";
            // 
            // tsslTotalProcess
            // 
            this.tsslTotalProcess.Name = "tsslTotalProcess";
            this.tsslTotalProcess.Size = new System.Drawing.Size(126, 17);
            this.tsslTotalProcess.Text = "Total active process:  0";
            // 
            // tsProgressBar
            // 
            this.tsProgressBar.Name = "tsProgressBar";
            this.tsProgressBar.Size = new System.Drawing.Size(100, 16);
            // 
            // tsddButton
            // 
            this.tsddButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.tsddButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cancelToolStripMenuItem});
            this.tsddButton.Image = ((System.Drawing.Image)(resources.GetObject("tsddButton.Image")));
            this.tsddButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsddButton.Name = "tsddButton";
            this.tsddButton.Size = new System.Drawing.Size(29, 20);
            // 
            // cancelToolStripMenuItem
            // 
            this.cancelToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("cancelToolStripMenuItem.Image")));
            this.cancelToolStripMenuItem.Name = "cancelToolStripMenuItem";
            this.cancelToolStripMenuItem.Size = new System.Drawing.Size(110, 22);
            this.cancelToolStripMenuItem.Text = "Cancel";
            this.cancelToolStripMenuItem.Click += new System.EventHandler(this.cancelBGW);
            // 
            // tsslCurrent
            // 
            this.tsslCurrent.Name = "tsslCurrent";
            this.tsslCurrent.Size = new System.Drawing.Size(10, 17);
            this.tsslCurrent.Text = " ";
            // 
            // tsslInfo
            // 
            this.tsslInfo.Name = "tsslInfo";
            this.tsslInfo.Size = new System.Drawing.Size(10, 17);
            this.tsslInfo.Text = " ";
            // 
            // notifyIcon
            // 
            this.notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.notifyIcon.BalloonTipTitle = "ImportProducts";
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "ImportProducts";
            this.notifyIcon.Visible = true;
            this.notifyIcon.BalloonTipClicked += new System.EventHandler(this.notifyIcon_BalloonTipClicked);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(763, 262);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "ImportProducts";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemFeed;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemProperties;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRun;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripProgressBar tsProgressBar;
        private System.Windows.Forms.ToolStripDropDownButton tsddButton;
        private System.Windows.Forms.ToolStripMenuItem cancelToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel tsslTotalProcess;
        private System.Windows.Forms.ToolStripStatusLabel tsslCurrent;
        private System.Windows.Forms.ToolStripStatusLabel tsslInfo;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ToolStripMenuItem toolStripDeleteProducts;
    }
}

