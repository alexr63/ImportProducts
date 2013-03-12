namespace ImportProducts
{
    partial class EditProperties
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
            this.label1 = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
            this.labelURL = new System.Windows.Forms.Label();
            this.textBoxURL = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.labelLastRun = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelName = new System.Windows.Forms.Label();
            this.labelCategory = new System.Windows.Forms.Label();
            this.labelPortalId = new System.Windows.Forms.Label();
            this.numericUpDownPortalId = new System.Windows.Forms.NumericUpDown();
            this.labelVendorId = new System.Windows.Forms.Label();
            this.numericUpDownVendorId = new System.Windows.Forms.NumericUpDown();
            this.labelAdvancedCategoryRoot = new System.Windows.Forms.Label();
            this.textBoxAdvancedCategoryRoot = new System.Windows.Forms.TextBox();
            this.labelCountryFilter = new System.Windows.Forms.Label();
            this.comboBoxCountry = new System.Windows.Forms.ComboBox();
            this.labelCityFilter = new System.Windows.Forms.Label();
            this.textBoxCity = new System.Windows.Forms.TextBox();
            this.comboBoxCategory = new System.Windows.Forms.ComboBox();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownPortalId)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownVendorId)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(38, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Name:";
            // 
            // buttonOK
            // 
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(216, 326);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 7;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.CausesValidation = false;
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(298, 326);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 8;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // errorProvider1
            // 
            this.errorProvider1.ContainerControl = this;
            // 
            // labelURL
            // 
            this.labelURL.AutoSize = true;
            this.labelURL.Location = new System.Drawing.Point(13, 44);
            this.labelURL.Name = "labelURL";
            this.labelURL.Size = new System.Drawing.Size(32, 13);
            this.labelURL.TabIndex = 2;
            this.labelURL.Text = "URL:";
            // 
            // textBoxURL
            // 
            this.textBoxURL.Location = new System.Drawing.Point(167, 37);
            this.textBoxURL.Name = "textBoxURL";
            this.textBoxURL.Size = new System.Drawing.Size(400, 20);
            this.textBoxURL.TabIndex = 0;
            this.textBoxURL.Validating += new System.ComponentModel.CancelEventHandler(this.textBoxURL_Validating);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 257);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 14;
            this.label3.Text = "Last Run:";
            // 
            // labelLastRun
            // 
            this.labelLastRun.AutoSize = true;
            this.labelLastRun.Location = new System.Drawing.Point(164, 257);
            this.labelLastRun.Name = "labelLastRun";
            this.labelLastRun.Size = new System.Drawing.Size(0, 13);
            this.labelLastRun.TabIndex = 15;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(13, 288);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(40, 13);
            this.label4.TabIndex = 16;
            this.label4.Text = "Status:";
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(164, 288);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(0, 13);
            this.labelStatus.TabIndex = 17;
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(164, 13);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(0, 13);
            this.labelName.TabIndex = 1;
            // 
            // labelCategory
            // 
            this.labelCategory.AutoSize = true;
            this.labelCategory.Location = new System.Drawing.Point(13, 75);
            this.labelCategory.Name = "labelCategory";
            this.labelCategory.Size = new System.Drawing.Size(49, 13);
            this.labelCategory.TabIndex = 4;
            this.labelCategory.Text = "Category";
            // 
            // labelPortalId
            // 
            this.labelPortalId.AutoSize = true;
            this.labelPortalId.Location = new System.Drawing.Point(13, 106);
            this.labelPortalId.Name = "labelPortalId";
            this.labelPortalId.Size = new System.Drawing.Size(43, 13);
            this.labelPortalId.TabIndex = 6;
            this.labelPortalId.Text = "PortalId";
            // 
            // numericUpDownPortalId
            // 
            this.numericUpDownPortalId.Location = new System.Drawing.Point(167, 99);
            this.numericUpDownPortalId.Name = "numericUpDownPortalId";
            this.numericUpDownPortalId.Size = new System.Drawing.Size(120, 20);
            this.numericUpDownPortalId.TabIndex = 2;
            // 
            // labelVendorId
            // 
            this.labelVendorId.AutoSize = true;
            this.labelVendorId.Location = new System.Drawing.Point(13, 137);
            this.labelVendorId.Name = "labelVendorId";
            this.labelVendorId.Size = new System.Drawing.Size(50, 13);
            this.labelVendorId.TabIndex = 8;
            this.labelVendorId.Text = "VendorId";
            // 
            // numericUpDownVendorId
            // 
            this.numericUpDownVendorId.Location = new System.Drawing.Point(167, 130);
            this.numericUpDownVendorId.Name = "numericUpDownVendorId";
            this.numericUpDownVendorId.Size = new System.Drawing.Size(120, 20);
            this.numericUpDownVendorId.TabIndex = 3;
            // 
            // labelAdvancedCategoryRoot
            // 
            this.labelAdvancedCategoryRoot.AutoSize = true;
            this.labelAdvancedCategoryRoot.Location = new System.Drawing.Point(13, 168);
            this.labelAdvancedCategoryRoot.Name = "labelAdvancedCategoryRoot";
            this.labelAdvancedCategoryRoot.Size = new System.Drawing.Size(127, 13);
            this.labelAdvancedCategoryRoot.TabIndex = 10;
            this.labelAdvancedCategoryRoot.Text = "Advanced Category Root";
            // 
            // textBoxAdvancedCategoryRoot
            // 
            this.textBoxAdvancedCategoryRoot.Location = new System.Drawing.Point(167, 161);
            this.textBoxAdvancedCategoryRoot.Name = "textBoxAdvancedCategoryRoot";
            this.textBoxAdvancedCategoryRoot.Size = new System.Drawing.Size(100, 20);
            this.textBoxAdvancedCategoryRoot.TabIndex = 4;
            this.textBoxAdvancedCategoryRoot.Validating += new System.ComponentModel.CancelEventHandler(this.textBoxAdvancedCategoryRoot_Validating);
            // 
            // labelCountryFilter
            // 
            this.labelCountryFilter.AutoSize = true;
            this.labelCountryFilter.Location = new System.Drawing.Point(13, 198);
            this.labelCountryFilter.Name = "labelCountryFilter";
            this.labelCountryFilter.Size = new System.Drawing.Size(68, 13);
            this.labelCountryFilter.TabIndex = 12;
            this.labelCountryFilter.Text = "Country Filter";
            // 
            // comboBoxCountry
            // 
            this.comboBoxCountry.DisplayMember = "Name";
            this.comboBoxCountry.FormattingEnabled = true;
            this.comboBoxCountry.Location = new System.Drawing.Point(167, 190);
            this.comboBoxCountry.Name = "comboBoxCountry";
            this.comboBoxCountry.Size = new System.Drawing.Size(121, 21);
            this.comboBoxCountry.TabIndex = 5;
            this.comboBoxCountry.ValueMember = "Id";
            // 
            // labelCityFilter
            // 
            this.labelCityFilter.AutoSize = true;
            this.labelCityFilter.Location = new System.Drawing.Point(13, 226);
            this.labelCityFilter.Name = "labelCityFilter";
            this.labelCityFilter.Size = new System.Drawing.Size(49, 13);
            this.labelCityFilter.TabIndex = 20;
            this.labelCityFilter.Text = "City Filter";
            // 
            // textBoxCity
            // 
            this.textBoxCity.Location = new System.Drawing.Point(167, 223);
            this.textBoxCity.Name = "textBoxCity";
            this.textBoxCity.Size = new System.Drawing.Size(100, 20);
            this.textBoxCity.TabIndex = 6;
            // 
            // comboBoxCategory
            // 
            this.comboBoxCategory.DisplayMember = "Name";
            this.comboBoxCategory.FormattingEnabled = true;
            this.comboBoxCategory.Location = new System.Drawing.Point(167, 66);
            this.comboBoxCategory.Name = "comboBoxCategory";
            this.comboBoxCategory.Size = new System.Drawing.Size(121, 21);
            this.comboBoxCategory.TabIndex = 1;
            this.comboBoxCategory.ValueMember = "Id";
            this.comboBoxCategory.Validating += new System.ComponentModel.CancelEventHandler(this.comboBoxCategory_Validating);
            // 
            // EditProperties
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(589, 359);
            this.Controls.Add(this.comboBoxCategory);
            this.Controls.Add(this.textBoxCity);
            this.Controls.Add(this.labelCityFilter);
            this.Controls.Add(this.comboBoxCountry);
            this.Controls.Add(this.labelCountryFilter);
            this.Controls.Add(this.textBoxAdvancedCategoryRoot);
            this.Controls.Add(this.labelAdvancedCategoryRoot);
            this.Controls.Add(this.numericUpDownVendorId);
            this.Controls.Add(this.labelVendorId);
            this.Controls.Add(this.numericUpDownPortalId);
            this.Controls.Add(this.labelPortalId);
            this.Controls.Add(this.labelCategory);
            this.Controls.Add(this.labelName);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.labelLastRun);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textBoxURL);
            this.Controls.Add(this.labelURL);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.label1);
            this.Name = "EditProperties";
            this.Text = "EditProperties";
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownPortalId)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownVendorId)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.ErrorProvider errorProvider1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label labelURL;
        public System.Windows.Forms.Label labelStatus;
        public System.Windows.Forms.Label labelLastRun;
        public System.Windows.Forms.TextBox textBoxURL;
        public System.Windows.Forms.Label labelName;
        private System.Windows.Forms.Label labelPortalId;
        private System.Windows.Forms.Label labelCategory;
        public System.Windows.Forms.NumericUpDown numericUpDownPortalId;
        private System.Windows.Forms.Label labelVendorId;
        public System.Windows.Forms.NumericUpDown numericUpDownVendorId;
        private System.Windows.Forms.Label labelAdvancedCategoryRoot;
        public System.Windows.Forms.TextBox textBoxAdvancedCategoryRoot;
        private System.Windows.Forms.Label labelCountryFilter;
        public System.Windows.Forms.ComboBox comboBoxCountry;
        private System.Windows.Forms.Label labelCityFilter;
        public System.Windows.Forms.TextBox textBoxCity;
        public System.Windows.Forms.ComboBox comboBoxCategory;
    }
}