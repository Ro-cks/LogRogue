namespace LogRogue.App
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            labelSource = new Label();
            txtSourcePath = new TextBox();
            btnBrowseSource = new Button();
            btnBrowseOutput = new Button();
            txtOutputPath = new TextBox();
            labelOutput = new Label();
            dtpEndDate = new DateTimePicker();
            dtpStartDate = new DateTimePicker();
            labelStartDate = new Label();
            labelEndDate = new Label();
            chkDeleteSource = new CheckBox();
            btnRun = new Button();
            lblStatus = new Label();
            folderDialog = new FolderBrowserDialog();
            lvResults = new ListView();
            cboDeleteMode = new ComboBox();
            chkAutoStart = new CheckBox();
            SuspendLayout();
            // 
            // labelSource
            // 
            labelSource.AutoSize = true;
            labelSource.Location = new Point(145, 41);
            labelSource.Name = "labelSource";
            labelSource.Size = new Size(59, 15);
            labelSource.TabIndex = 0;
            labelSource.Text = "대상 폴더";
            // 
            // txtSourcePath
            // 
            txtSourcePath.Location = new Point(219, 38);
            txtSourcePath.Name = "txtSourcePath";
            txtSourcePath.Size = new Size(356, 23);
            txtSourcePath.TabIndex = 1;
            // 
            // btnBrowseSource
            // 
            btnBrowseSource.Location = new Point(581, 37);
            btnBrowseSource.Name = "btnBrowseSource";
            btnBrowseSource.Size = new Size(75, 23);
            btnBrowseSource.TabIndex = 2;
            btnBrowseSource.Text = "찾아보기";
            btnBrowseSource.UseVisualStyleBackColor = true;
            btnBrowseSource.Click += btnBrowseSource_Click;
            // 
            // btnBrowseOutput
            // 
            btnBrowseOutput.Location = new Point(581, 66);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(75, 23);
            btnBrowseOutput.TabIndex = 5;
            btnBrowseOutput.Text = "찾아보기";
            btnBrowseOutput.UseVisualStyleBackColor = true;
            btnBrowseOutput.Click += btnBrowseOutput_Click;
            // 
            // txtOutputPath
            // 
            txtOutputPath.Location = new Point(219, 67);
            txtOutputPath.Name = "txtOutputPath";
            txtOutputPath.Size = new Size(356, 23);
            txtOutputPath.TabIndex = 4;
            // 
            // labelOutput
            // 
            labelOutput.AutoSize = true;
            labelOutput.Location = new Point(145, 70);
            labelOutput.Name = "labelOutput";
            labelOutput.Size = new Size(59, 15);
            labelOutput.TabIndex = 3;
            labelOutput.Text = "출력 폴더";
            // 
            // dtpEndDate
            // 
            dtpEndDate.Location = new Point(456, 131);
            dtpEndDate.Name = "dtpEndDate";
            dtpEndDate.Size = new Size(200, 23);
            dtpEndDate.TabIndex = 6;
            // 
            // dtpStartDate
            // 
            dtpStartDate.Location = new Point(145, 131);
            dtpStartDate.Name = "dtpStartDate";
            dtpStartDate.Size = new Size(200, 23);
            dtpStartDate.TabIndex = 7;
            // 
            // labelStartDate
            // 
            labelStartDate.AutoSize = true;
            labelStartDate.Location = new Point(145, 113);
            labelStartDate.Name = "labelStartDate";
            labelStartDate.Size = new Size(43, 15);
            labelStartDate.TabIndex = 8;
            labelStartDate.Text = "시작일";
            // 
            // labelEndDate
            // 
            labelEndDate.AutoSize = true;
            labelEndDate.Location = new Point(456, 113);
            labelEndDate.Name = "labelEndDate";
            labelEndDate.Size = new Size(43, 15);
            labelEndDate.TabIndex = 9;
            labelEndDate.Text = "종료일";
            // 
            // chkDeleteSource
            // 
            chkDeleteSource.AutoSize = true;
            chkDeleteSource.Location = new Point(370, 176);
            chkDeleteSource.Name = "chkDeleteSource";
            chkDeleteSource.Size = new Size(78, 19);
            chkDeleteSource.TabIndex = 10;
            chkDeleteSource.Text = "원본 삭제";
            chkDeleteSource.UseVisualStyleBackColor = true;
            // 
            // btnRun
            // 
            btnRun.Location = new Point(581, 174);
            btnRun.Name = "btnRun";
            btnRun.Size = new Size(75, 23);
            btnRun.TabIndex = 11;
            btnRun.Text = "실행";
            btnRun.UseVisualStyleBackColor = true;
            btnRun.Click += btnRun_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(343, 199);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 15);
            lblStatus.TabIndex = 12;
            lblStatus.Text = "label1";
            // 
            // lvResults
            // 
            lvResults.Location = new Point(145, 252);
            lvResults.Name = "lvResults";
            lvResults.Size = new Size(511, 162);
            lvResults.TabIndex = 13;
            lvResults.UseCompatibleStateImageBehavior = false;
            // 
            // cboDeleteMode
            // 
            cboDeleteMode.FormattingEnabled = true;
            cboDeleteMode.Location = new Point(454, 174);
            cboDeleteMode.Name = "cboDeleteMode";
            cboDeleteMode.Size = new Size(121, 23);
            cboDeleteMode.TabIndex = 14;
            // 
            // chkAutoStart
            // 
            chkAutoStart.AutoSize = true;
            chkAutoStart.Location = new Point(481, 12);
            chkAutoStart.Name = "chkAutoStart";
            chkAutoStart.Size = new Size(175, 19);
            chkAutoStart.TabIndex = 15;
            chkAutoStart.Text = "Windows 시작 시 자동 실행";
            chkAutoStart.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(chkAutoStart);
            Controls.Add(cboDeleteMode);
            Controls.Add(lvResults);
            Controls.Add(lblStatus);
            Controls.Add(btnRun);
            Controls.Add(chkDeleteSource);
            Controls.Add(labelEndDate);
            Controls.Add(labelStartDate);
            Controls.Add(dtpStartDate);
            Controls.Add(dtpEndDate);
            Controls.Add(btnBrowseOutput);
            Controls.Add(txtOutputPath);
            Controls.Add(labelOutput);
            Controls.Add(btnBrowseSource);
            Controls.Add(txtSourcePath);
            Controls.Add(labelSource);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelSource;
        private TextBox txtSourcePath;
        private Button btnBrowseSource;
        private Button btnBrowseOutput;
        private TextBox txtOutputPath;
        private Label labelOutput;
        private DateTimePicker dtpEndDate;
        private DateTimePicker dtpStartDate;
        private Label labelStartDate;
        private Label labelEndDate;
        private CheckBox chkDeleteSource;
        private Button btnRun;
        private Label lblStatus;
        private FolderBrowserDialog folderDialog;
        private ListView lvResults;
        private ComboBox cboDeleteMode;
        private CheckBox chkAutoStart;
    }
}
