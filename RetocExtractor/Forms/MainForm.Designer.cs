namespace RetocExtractor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblProfile;
        private System.Windows.Forms.Label lblPaks;
        private System.Windows.Forms.Label lblAesKey;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.FlowLayoutPanel pnlExportHistory;
        private System.Windows.Forms.Label lblFolderHeader;
        private System.Windows.Forms.Label lblAssetsHeader;
        private System.Windows.Forms.TextBox txtPaks;
        private System.Windows.Forms.TextBox txtAesKey;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ComboBox cmbVersion;
        private System.Windows.Forms.ComboBox cmbProfile;
        private System.Windows.Forms.TreeView trvFolders;
        private System.Windows.Forms.ListView lstAssets;
        private System.Windows.Forms.ColumnHeader colAssetName;
        private System.Windows.Forms.ColumnHeader colAssetSize;
        private System.Windows.Forms.SplitContainer splitBrowser;
        private System.Windows.Forms.SplitContainer splitPackagesHistory;
        private System.Windows.Forms.Button btnBrowsePaks;
        private System.Windows.Forms.Button btnNewProfile;
        private System.Windows.Forms.Button btnDeleteProfile;
        private System.Windows.Forms.ContextMenuStrip contextMenuExport;
        private System.Windows.Forms.ToolStripMenuItem menuExportUassetOnly;
        private System.Windows.Forms.ToolStripMenuItem menuExportUexpOnly;
        private System.Windows.Forms.ToolStripMenuItem menuExportBoth;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
            if (disposing && exportHistoryLinkFont != null)
            {
                exportHistoryLinkFont.Dispose();
                exportHistoryLinkFont = null;
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblProfile = new System.Windows.Forms.Label();
            this.lblPaks = new System.Windows.Forms.Label();
            this.lblAesKey = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblSearch = new System.Windows.Forms.Label();
            this.pnlExportHistory = new System.Windows.Forms.FlowLayoutPanel();
            this.lblFolderHeader = new System.Windows.Forms.Label();
            this.lblAssetsHeader = new System.Windows.Forms.Label();
            this.txtPaks = new System.Windows.Forms.TextBox();
            this.txtAesKey = new System.Windows.Forms.TextBox();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.cmbVersion = new System.Windows.Forms.ComboBox();
            this.cmbProfile = new System.Windows.Forms.ComboBox();
            this.trvFolders = new System.Windows.Forms.TreeView();
            this.contextMenuExport = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuExportBoth = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportUassetOnly = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportUexpOnly = new System.Windows.Forms.ToolStripMenuItem();
            this.lstAssets = new System.Windows.Forms.ListView();
            this.colAssetName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colAssetSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.splitPackagesHistory = new System.Windows.Forms.SplitContainer();
            this.lblExportHistory = new System.Windows.Forms.Label();
            this.splitBrowser = new System.Windows.Forms.SplitContainer();
            this.btnBrowsePaks = new System.Windows.Forms.Button();
            this.btnNewProfile = new System.Windows.Forms.Button();
            this.btnDeleteProfile = new System.Windows.Forms.Button();
            this.txt_version = new System.Windows.Forms.Label();
            this.contextMenuExport.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitPackagesHistory)).BeginInit();
            this.splitPackagesHistory.Panel1.SuspendLayout();
            this.splitPackagesHistory.Panel2.SuspendLayout();
            this.splitPackagesHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitBrowser)).BeginInit();
            this.splitBrowser.Panel1.SuspendLayout();
            this.splitBrowser.Panel2.SuspendLayout();
            this.splitBrowser.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblProfile
            // 
            this.lblProfile.Location = new System.Drawing.Point(14, 8);
            this.lblProfile.Name = "lblProfile";
            this.lblProfile.Size = new System.Drawing.Size(70, 24);
            this.lblProfile.TabIndex = 0;
            this.lblProfile.Text = "Profile:";
            this.lblProfile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPaks
            // 
            this.lblPaks.Location = new System.Drawing.Point(14, 39);
            this.lblPaks.Name = "lblPaks";
            this.lblPaks.Size = new System.Drawing.Size(70, 24);
            this.lblPaks.TabIndex = 6;
            this.lblPaks.Text = "Paks folder:";
            this.lblPaks.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAesKey
            // 
            this.lblAesKey.Location = new System.Drawing.Point(14, 71);
            this.lblAesKey.Name = "lblAesKey";
            this.lblAesKey.Size = new System.Drawing.Size(70, 24);
            this.lblAesKey.TabIndex = 12;
            this.lblAesKey.Text = "AES key:";
            this.lblAesKey.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblVersion
            // 
            this.lblVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblVersion.Location = new System.Drawing.Point(454, 8);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(66, 24);
            this.lblVersion.TabIndex = 14;
            this.lblVersion.Text = "UE Version:";
            this.lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblSearch
            // 
            this.lblSearch.Location = new System.Drawing.Point(549, 72);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(45, 24);
            this.lblSearch.TabIndex = 17;
            this.lblSearch.Text = "Search:";
            this.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pnlExportHistory
            // 
            this.pnlExportHistory.AutoScroll = true;
            this.pnlExportHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlExportHistory.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlExportHistory.Location = new System.Drawing.Point(0, 22);
            this.pnlExportHistory.Name = "pnlExportHistory";
            this.pnlExportHistory.Padding = new System.Windows.Forms.Padding(2);
            this.pnlExportHistory.Size = new System.Drawing.Size(668, 114);
            this.pnlExportHistory.TabIndex = 29;
            this.pnlExportHistory.WrapContents = false;
            // 
            // lblFolderHeader
            // 
            this.lblFolderHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblFolderHeader.Location = new System.Drawing.Point(0, 0);
            this.lblFolderHeader.Name = "lblFolderHeader";
            this.lblFolderHeader.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.lblFolderHeader.Size = new System.Drawing.Size(300, 24);
            this.lblFolderHeader.TabIndex = 0;
            this.lblFolderHeader.Text = "Folders";
            this.lblFolderHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAssetsHeader
            // 
            this.lblAssetsHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblAssetsHeader.Location = new System.Drawing.Point(0, 0);
            this.lblAssetsHeader.Name = "lblAssetsHeader";
            this.lblAssetsHeader.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.lblAssetsHeader.Size = new System.Drawing.Size(668, 24);
            this.lblAssetsHeader.TabIndex = 0;
            this.lblAssetsHeader.Text = "Packages";
            this.lblAssetsHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtPaks
            // 
            this.txtPaks.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtPaks.Location = new System.Drawing.Point(88, 42);
            this.txtPaks.Name = "txtPaks";
            this.txtPaks.Size = new System.Drawing.Size(782, 23);
            this.txtPaks.TabIndex = 7;
            // 
            // txtAesKey
            // 
            this.txtAesKey.Location = new System.Drawing.Point(88, 73);
            this.txtAesKey.MaxLength = 66;
            this.txtAesKey.Name = "txtAesKey";
            this.txtAesKey.Size = new System.Drawing.Size(432, 23);
            this.txtAesKey.TabIndex = 13;
            // 
            // txtSearch
            // 
            this.txtSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearch.Location = new System.Drawing.Point(600, 73);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(270, 23);
            this.txtSearch.TabIndex = 18;
            // 
            // cmbVersion
            // 
            this.cmbVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbVersion.FormattingEnabled = true;
            this.cmbVersion.Location = new System.Drawing.Point(526, 9);
            this.cmbVersion.Name = "cmbVersion";
            this.cmbVersion.Size = new System.Drawing.Size(76, 23);
            this.cmbVersion.TabIndex = 15;
            // 
            // cmbProfile
            // 
            this.cmbProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProfile.FormattingEnabled = true;
            this.cmbProfile.Location = new System.Drawing.Point(88, 11);
            this.cmbProfile.Name = "cmbProfile";
            this.cmbProfile.Size = new System.Drawing.Size(160, 23);
            this.cmbProfile.TabIndex = 1;
            // 
            // trvFolders
            // 
            this.trvFolders.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.trvFolders.ContextMenuStrip = this.contextMenuExport;
            this.trvFolders.Dock = System.Windows.Forms.DockStyle.Fill;
            this.trvFolders.HideSelection = false;
            this.trvFolders.Location = new System.Drawing.Point(0, 24);
            this.trvFolders.Name = "trvFolders";
            this.trvFolders.Size = new System.Drawing.Size(300, 536);
            this.trvFolders.TabIndex = 1;
            // 
            // contextMenuExport
            // 
            this.contextMenuExport.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuExportBoth,
            this.menuExportUassetOnly,
            this.menuExportUexpOnly});
            this.contextMenuExport.Name = "contextMenuExport";
            this.contextMenuExport.Size = new System.Drawing.Size(248, 70);
            // 
            // menuExportBoth
            // 
            this.menuExportBoth.Name = "menuExportBoth";
            this.menuExportBoth.Size = new System.Drawing.Size(247, 22);
            this.menuExportBoth.Text = "Export Raw data (.uasset + .uexp)";
            // 
            // menuExportUassetOnly
            // 
            this.menuExportUassetOnly.Name = "menuExportUassetOnly";
            this.menuExportUassetOnly.Size = new System.Drawing.Size(247, 22);
            this.menuExportUassetOnly.Text = "Export only .uasset";
            // 
            // menuExportUexpOnly
            // 
            this.menuExportUexpOnly.Name = "menuExportUexpOnly";
            this.menuExportUexpOnly.Size = new System.Drawing.Size(247, 22);
            this.menuExportUexpOnly.Text = "Export only .uexp";
            // 
            // lstAssets
            // 
            this.lstAssets.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstAssets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colAssetName,
            this.colAssetSize});
            this.lstAssets.ContextMenuStrip = this.contextMenuExport;
            this.lstAssets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstAssets.FullRowSelect = true;
            this.lstAssets.HideSelection = false;
            this.lstAssets.Location = new System.Drawing.Point(0, 24);
            this.lstAssets.Name = "lstAssets";
            this.lstAssets.Size = new System.Drawing.Size(668, 396);
            this.lstAssets.TabIndex = 1;
            this.lstAssets.UseCompatibleStateImageBehavior = false;
            this.lstAssets.View = System.Windows.Forms.View.Details;
            // 
            // colAssetName
            // 
            this.colAssetName.Text = "Asset name";
            this.colAssetName.Width = 700;
            // 
            // colAssetSize
            // 
            this.colAssetSize.Text = "Size";
            this.colAssetSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colAssetSize.Width = 86;
            // 
            // splitPackagesHistory
            // 
            this.splitPackagesHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitPackagesHistory.Location = new System.Drawing.Point(0, 0);
            this.splitPackagesHistory.Name = "splitPackagesHistory";
            this.splitPackagesHistory.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitPackagesHistory.Panel1
            // 
            this.splitPackagesHistory.Panel1.Controls.Add(this.lstAssets);
            this.splitPackagesHistory.Panel1.Controls.Add(this.lblAssetsHeader);
            this.splitPackagesHistory.Panel1MinSize = 220;
            // 
            // splitPackagesHistory.Panel2
            // 
            this.splitPackagesHistory.Panel2.Controls.Add(this.pnlExportHistory);
            this.splitPackagesHistory.Panel2.Controls.Add(this.lblExportHistory);
            this.splitPackagesHistory.Panel2MinSize = 88;
            this.splitPackagesHistory.Size = new System.Drawing.Size(668, 560);
            this.splitPackagesHistory.SplitterDistance = 420;
            this.splitPackagesHistory.TabIndex = 30;
            // 
            // lblExportHistory
            // 
            this.lblExportHistory.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblExportHistory.Location = new System.Drawing.Point(0, 0);
            this.lblExportHistory.Name = "lblExportHistory";
            this.lblExportHistory.Size = new System.Drawing.Size(668, 22);
            this.lblExportHistory.TabIndex = 28;
            this.lblExportHistory.Text = "Export history — click an asset to open its folder";
            this.lblExportHistory.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // splitBrowser
            // 
            this.splitBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitBrowser.Location = new System.Drawing.Point(14, 109);
            this.splitBrowser.Name = "splitBrowser";
            // 
            // splitBrowser.Panel1
            // 
            this.splitBrowser.Panel1.Controls.Add(this.trvFolders);
            this.splitBrowser.Panel1.Controls.Add(this.lblFolderHeader);
            this.splitBrowser.Panel1MinSize = 240;
            // 
            // splitBrowser.Panel2
            // 
            this.splitBrowser.Panel2.Controls.Add(this.splitPackagesHistory);
            this.splitBrowser.Panel2MinSize = 450;
            this.splitBrowser.Size = new System.Drawing.Size(972, 560);
            this.splitBrowser.SplitterDistance = 300;
            this.splitBrowser.TabIndex = 24;
            // 
            // btnBrowsePaks
            // 
            this.btnBrowsePaks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowsePaks.Location = new System.Drawing.Point(878, 40);
            this.btnBrowsePaks.Name = "btnBrowsePaks";
            this.btnBrowsePaks.Size = new System.Drawing.Size(106, 26);
            this.btnBrowsePaks.TabIndex = 8;
            this.btnBrowsePaks.Text = "Browse...";
            this.btnBrowsePaks.UseVisualStyleBackColor = true;
            // 
            // btnNewProfile
            // 
            this.btnNewProfile.Location = new System.Drawing.Point(258, 8);
            this.btnNewProfile.Name = "btnNewProfile";
            this.btnNewProfile.Size = new System.Drawing.Size(92, 26);
            this.btnNewProfile.TabIndex = 2;
            this.btnNewProfile.Text = "New profile...";
            this.btnNewProfile.UseVisualStyleBackColor = true;
            // 
            // btnDeleteProfile
            // 
            this.btnDeleteProfile.Location = new System.Drawing.Point(356, 8);
            this.btnDeleteProfile.Name = "btnDeleteProfile";
            this.btnDeleteProfile.Size = new System.Drawing.Size(92, 26);
            this.btnDeleteProfile.TabIndex = 3;
            this.btnDeleteProfile.Text = "Delete profile";
            this.btnDeleteProfile.UseVisualStyleBackColor = true;
            // 
            // txt_version
            // 
            this.txt_version.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.txt_version.AutoSize = true;
            this.txt_version.Location = new System.Drawing.Point(952, 678);
            this.txt_version.Name = "txt_version";
            this.txt_version.Size = new System.Drawing.Size(31, 15);
            this.txt_version.TabIndex = 26;
            this.txt_version.Text = "1.0.1";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 700);
            this.Controls.Add(this.txt_version);
            this.Controls.Add(this.splitBrowser);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.lblSearch);
            this.Controls.Add(this.cmbVersion);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.txtAesKey);
            this.Controls.Add(this.lblAesKey);
            this.Controls.Add(this.btnBrowsePaks);
            this.Controls.Add(this.txtPaks);
            this.Controls.Add(this.lblPaks);
            this.Controls.Add(this.btnDeleteProfile);
            this.Controls.Add(this.btnNewProfile);
            this.Controls.Add(this.cmbProfile);
            this.Controls.Add(this.lblProfile);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinimumSize = new System.Drawing.Size(860, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Retoc Extractor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.contextMenuExport.ResumeLayout(false);
            this.splitPackagesHistory.Panel1.ResumeLayout(false);
            this.splitPackagesHistory.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitPackagesHistory)).EndInit();
            this.splitPackagesHistory.ResumeLayout(false);
            this.splitBrowser.Panel1.ResumeLayout(false);
            this.splitBrowser.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitBrowser)).EndInit();
            this.splitBrowser.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblExportHistory;
        private System.Windows.Forms.Label txt_version;
    }
}
