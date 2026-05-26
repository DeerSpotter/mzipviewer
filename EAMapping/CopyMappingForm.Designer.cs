namespace EAMapping
{
    partial class CopyMappingForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CopyMappingForm));
            this.filterTextBox = new System.Windows.Forms.TextBox();
            this.filterTextBoxLabel = new System.Windows.Forms.Label();
            this.filterButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.copyButton = new System.Windows.Forms.Button();
            this.sourceObjectListView = new BrightIdeasSoftware.ObjectListView();
            this.sourceFQNColumn = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.isSelectedColumn = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.mappingNodeImageList = new System.Windows.Forms.ImageList(this.components);
            this.mappingToLabel = new System.Windows.Forms.Label();
            this.mappingToTextBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.sourceObjectListView)).BeginInit();
            this.SuspendLayout();
            // 
            // filterTextBox
            // 
            this.filterTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.filterTextBox.Location = new System.Drawing.Point(12, 64);
            this.filterTextBox.Name = "filterTextBox";
            this.filterTextBox.Size = new System.Drawing.Size(639, 20);
            this.filterTextBox.TabIndex = 0;
            // 
            // filterTextBoxLabel
            // 
            this.filterTextBoxLabel.AutoSize = true;
            this.filterTextBoxLabel.Location = new System.Drawing.Point(9, 48);
            this.filterTextBoxLabel.Name = "filterTextBoxLabel";
            this.filterTextBoxLabel.Size = new System.Drawing.Size(63, 13);
            this.filterTextBoxLabel.TabIndex = 1;
            this.filterTextBoxLabel.Text = "Regex Filter";
            // 
            // filterButton
            // 
            this.filterButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.filterButton.Location = new System.Drawing.Point(657, 62);
            this.filterButton.Name = "filterButton";
            this.filterButton.Size = new System.Drawing.Size(75, 23);
            this.filterButton.TabIndex = 2;
            this.filterButton.Text = "Filter";
            this.filterButton.UseVisualStyleBackColor = true;
            this.filterButton.Click += new System.EventHandler(this.filterButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(657, 561);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 17;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // copyButton
            // 
            this.copyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.copyButton.Location = new System.Drawing.Point(576, 561);
            this.copyButton.Name = "copyButton";
            this.copyButton.Size = new System.Drawing.Size(75, 23);
            this.copyButton.TabIndex = 16;
            this.copyButton.Text = "Copy";
            this.copyButton.UseVisualStyleBackColor = true;
            this.copyButton.Click += new System.EventHandler(this.copyButton_Click);
            // 
            // sourceTreeView
            // 
            this.sourceObjectListView.AllColumns.Add(this.sourceFQNColumn);
            this.sourceObjectListView.AllColumns.Add(this.isSelectedColumn);
            this.sourceObjectListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceObjectListView.CellEditUseWholeCell = false;
            this.sourceObjectListView.CheckedAspectName = "isSelected";
            this.sourceObjectListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.sourceFQNColumn,
            this.isSelectedColumn});
            this.sourceObjectListView.Cursor = System.Windows.Forms.Cursors.Default;
            this.sourceObjectListView.FullRowSelect = true;
            this.sourceObjectListView.GridLines = true;
            this.sourceObjectListView.HideSelection = false;
            this.sourceObjectListView.Location = new System.Drawing.Point(12, 91);
            this.sourceObjectListView.Name = "sourceTreeView";
            this.sourceObjectListView.ShowGroups = false;
            this.sourceObjectListView.ShowImagesOnSubItems = true;
            this.sourceObjectListView.Size = new System.Drawing.Size(720, 464);
            this.sourceObjectListView.SmallImageList = this.mappingNodeImageList;
            this.sourceObjectListView.TabIndex = 18;
            this.sourceObjectListView.UseCellFormatEvents = true;
            this.sourceObjectListView.UseCompatibleStateImageBehavior = false;
            this.sourceObjectListView.View = System.Windows.Forms.View.Details;
            this.sourceObjectListView.VirtualMode = false;
            // 
            // sourceFQNColumn
            // 
            this.sourceFQNColumn.AspectName = "mappingPathExportString";
            this.sourceFQNColumn.Hideable = false;
            this.sourceFQNColumn.Text = "Source Node";
            this.sourceFQNColumn.Width = 651;
            // 
            // isSelectedColumn
            // 
            this.isSelectedColumn.AspectName = "isSelected";
            this.isSelectedColumn.CheckBoxes = true;
            this.isSelectedColumn.HeaderCheckBox = true;
            this.isSelectedColumn.MaximumWidth = 200;
            this.isSelectedColumn.MinimumWidth = 200;
            this.isSelectedColumn.Text = "";
            this.isSelectedColumn.Width = 200;
            // 
            // mappingNodeImageList
            // 
            this.mappingNodeImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("mappingNodeImageList.ImageStream")));
            this.mappingNodeImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.mappingNodeImageList.Images.SetKeyName(0, "attributeNode");
            this.mappingNodeImageList.Images.SetKeyName(1, "classifierNode");
            this.mappingNodeImageList.Images.SetKeyName(2, "assocationNode");
            this.mappingNodeImageList.Images.SetKeyName(3, "packageNode");
            // 
            // mappingToLabel
            // 
            this.mappingToLabel.AutoSize = true;
            this.mappingToLabel.Location = new System.Drawing.Point(12, 9);
            this.mappingToLabel.Name = "mappingToLabel";
            this.mappingToLabel.Size = new System.Drawing.Size(87, 13);
            this.mappingToLabel.TabIndex = 20;
            this.mappingToLabel.Text = "Copy Mapping to";
            // 
            // mappingToTextBox
            // 
            this.mappingToTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mappingToTextBox.Location = new System.Drawing.Point(12, 25);
            this.mappingToTextBox.Name = "mappingToTextBox";
            this.mappingToTextBox.ReadOnly = true;
            this.mappingToTextBox.Size = new System.Drawing.Size(639, 20);
            this.mappingToTextBox.TabIndex = 19;
            // 
            // CopyMappingForm
            // 
            this.AcceptButton = this.filterButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(744, 596);
            this.Controls.Add(this.mappingToLabel);
            this.Controls.Add(this.mappingToTextBox);
            this.Controls.Add(this.sourceObjectListView);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.copyButton);
            this.Controls.Add(this.filterButton);
            this.Controls.Add(this.filterTextBoxLabel);
            this.Controls.Add(this.filterTextBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimizeBox = false;
            this.Name = "CopyMappingForm";
            this.Text = "Copy Mapping";
            ((System.ComponentModel.ISupportInitialize)(this.sourceObjectListView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox filterTextBox;
        private System.Windows.Forms.Label filterTextBoxLabel;
        private System.Windows.Forms.Button filterButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button copyButton;
        private BrightIdeasSoftware.ObjectListView sourceObjectListView;
        public BrightIdeasSoftware.OLVColumn sourceFQNColumn;
        private BrightIdeasSoftware.OLVColumn isSelectedColumn;
        private System.Windows.Forms.Label mappingToLabel;
        private System.Windows.Forms.TextBox mappingToTextBox;
        private System.Windows.Forms.ImageList mappingNodeImageList;
    }
}