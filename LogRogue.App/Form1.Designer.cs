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
            cboGrouping = new ComboBox();
            lblGrouping = new Label();
            cboPeriodMode = new ComboBox();
            nudKeepDays = new NumericUpDown();
            label1 = new Label();
            btnHistory = new Button();
            btnSchedule = new Button();
            ((System.ComponentModel.ISupportInitialize)nudKeepDays).BeginInit();
            SuspendLayout();
            // 
            // labelSource
            // 
            labelSource.AutoSize = true;
            labelSource.Location = new Point(99, 46);
            labelSource.Name = "labelSource";
            labelSource.Size = new Size(59, 15);
            labelSource.TabIndex = 0;
            labelSource.Text = "대상 폴더";
            // 
            // txtSourcePath
            // 
            txtSourcePath.Location = new Point(173, 43);
            txtSourcePath.Name = "txtSourcePath";
            txtSourcePath.Size = new Size(386, 23);
            txtSourcePath.TabIndex = 1;
            // 
            // btnBrowseSource
            // 
            btnBrowseSource.Location = new Point(565, 42);
            btnBrowseSource.Name = "btnBrowseSource";
            btnBrowseSource.Size = new Size(75, 23);
            btnBrowseSource.TabIndex = 2;
            btnBrowseSource.Text = "찾아보기";
            btnBrowseSource.UseVisualStyleBackColor = true;
            btnBrowseSource.Click += btnBrowseSource_Click;
            // 
            // btnBrowseOutput
            // 
            btnBrowseOutput.Location = new Point(565, 75);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(75, 23);
            btnBrowseOutput.TabIndex = 5;
            btnBrowseOutput.Text = "찾아보기";
            btnBrowseOutput.UseVisualStyleBackColor = true;
            btnBrowseOutput.Click += btnBrowseOutput_Click;
            // 
            // txtOutputPath
            // 
            txtOutputPath.Location = new Point(173, 72);
            txtOutputPath.Name = "txtOutputPath";
            txtOutputPath.Size = new Size(386, 23);
            txtOutputPath.TabIndex = 4;
            // 
            // labelOutput
            // 
            labelOutput.AutoSize = true;
            labelOutput.Location = new Point(99, 75);
            labelOutput.Name = "labelOutput";
            labelOutput.Size = new Size(59, 15);
            labelOutput.TabIndex = 3;
            labelOutput.Text = "출력 폴더";
            // 
            // dtpEndDate
            // 
            dtpEndDate.Location = new Point(440, 135);
            dtpEndDate.Name = "dtpEndDate";
            dtpEndDate.Size = new Size(200, 23);
            dtpEndDate.TabIndex = 6;
            // 
            // dtpStartDate
            // 
            dtpStartDate.Location = new Point(234, 135);
            dtpStartDate.Name = "dtpStartDate";
            dtpStartDate.Size = new Size(200, 23);
            dtpStartDate.TabIndex = 7;
            // 
            // labelStartDate
            // 
            labelStartDate.AutoSize = true;
            labelStartDate.Location = new Point(234, 117);
            labelStartDate.Name = "labelStartDate";
            labelStartDate.Size = new Size(43, 15);
            labelStartDate.TabIndex = 8;
            labelStartDate.Text = "시작일";
            // 
            // labelEndDate
            // 
            labelEndDate.AutoSize = true;
            labelEndDate.Location = new Point(440, 117);
            labelEndDate.Name = "labelEndDate";
            labelEndDate.Size = new Size(43, 15);
            labelEndDate.TabIndex = 9;
            labelEndDate.Text = "종료일";
            // 
            // chkDeleteSource
            // 
            chkDeleteSource.AutoSize = true;
            chkDeleteSource.Location = new Point(229, 221);
            chkDeleteSource.Name = "chkDeleteSource";
            chkDeleteSource.Size = new Size(78, 19);
            chkDeleteSource.TabIndex = 10;
            chkDeleteSource.Text = "원본 삭제";
            chkDeleteSource.UseVisualStyleBackColor = true;
            // 
            // btnRun
            // 
            btnRun.Location = new Point(565, 248);
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
            lblStatus.Location = new Point(99, 276);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 15);
            lblStatus.TabIndex = 12;
            lblStatus.Text = "label1";
            // 
            // lvResults
            // 
            lvResults.Location = new Point(99, 319);
            lvResults.Name = "lvResults";
            lvResults.Size = new Size(541, 177);
            lvResults.TabIndex = 13;
            lvResults.UseCompatibleStateImageBehavior = false;
            // 
            // cboDeleteMode
            // 
            cboDeleteMode.FormattingEnabled = true;
            cboDeleteMode.Location = new Point(313, 219);
            cboDeleteMode.Name = "cboDeleteMode";
            cboDeleteMode.Size = new Size(121, 23);
            cboDeleteMode.TabIndex = 14;
            // 
            // chkAutoStart
            // 
            chkAutoStart.AutoSize = true;
            chkAutoStart.Location = new Point(465, 17);
            chkAutoStart.Name = "chkAutoStart";
            chkAutoStart.Size = new Size(175, 19);
            chkAutoStart.TabIndex = 15;
            chkAutoStart.Text = "Windows 시작 시 자동 실행";
            chkAutoStart.UseVisualStyleBackColor = true;
            // 
            // cboGrouping
            // 
            cboGrouping.FormattingEnabled = true;
            cboGrouping.Location = new Point(99, 221);
            cboGrouping.Name = "cboGrouping";
            cboGrouping.Size = new Size(121, 23);
            cboGrouping.TabIndex = 16;
            // 
            // lblGrouping
            // 
            lblGrouping.AutoSize = true;
            lblGrouping.Location = new Point(99, 202);
            lblGrouping.Name = "lblGrouping";
            lblGrouping.Size = new Size(59, 15);
            lblGrouping.TabIndex = 17;
            lblGrouping.Text = "압축 단위";
            // 
            // cboPeriodMode
            // 
            cboPeriodMode.FormattingEnabled = true;
            cboPeriodMode.Location = new Point(99, 149);
            cboPeriodMode.Name = "cboPeriodMode";
            cboPeriodMode.Size = new Size(121, 23);
            cboPeriodMode.TabIndex = 18;
            // 
            // nudKeepDays
            // 
            nudKeepDays.Location = new Point(234, 164);
            nudKeepDays.Name = "nudKeepDays";
            nudKeepDays.Size = new Size(120, 23);
            nudKeepDays.TabIndex = 19;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(360, 166);
            label1.Name = "label1";
            label1.Size = new Size(249, 15);
            label1.TabIndex = 20;
            label1.Text = "설정된 날은 남기고, 이전 날들을 포함합니다.";
            // 
            // btnHistory
            // 
            btnHistory.Location = new Point(465, 219);
            btnHistory.Name = "btnHistory";
            btnHistory.Size = new Size(75, 23);
            btnHistory.TabIndex = 21;
            btnHistory.Text = "작업 이력";
            btnHistory.UseVisualStyleBackColor = true;
            // 
            // btnSchedule
            // 
            btnSchedule.Location = new Point(465, 248);
            btnSchedule.Name = "btnSchedule";
            btnSchedule.Size = new Size(75, 23);
            btnSchedule.TabIndex = 22;
            btnSchedule.Text = "예약 설정";
            btnSchedule.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(739, 512);
            Controls.Add(btnSchedule);
            Controls.Add(btnHistory);
            Controls.Add(label1);
            Controls.Add(nudKeepDays);
            Controls.Add(cboPeriodMode);
            Controls.Add(lblGrouping);
            Controls.Add(cboGrouping);
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
            Text = "Log Rogue";
            ((System.ComponentModel.ISupportInitialize)nudKeepDays).EndInit();
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
        private ComboBox cboGrouping;
        private Label lblGrouping;
        private ComboBox cboPeriodMode;
        private NumericUpDown nudKeepDays;
        private Label label1;
        private Button btnHistory;
        private Button btnSchedule;
    }
}
