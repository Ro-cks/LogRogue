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
            lblStatus = new Label();
            lvResults = new ListView();
            chkAutoStart = new CheckBox();
            btnHistory = new Button();
            btnAbout = new Button();
            lvJobs = new ListView();
            btnAddJob = new Button();
            btnEditJob = new Button();
            btnRemoveJob = new Button();
            btnRunJob = new Button();
            SuspendLayout();
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(99, 276);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(541, 40);
            lblStatus.TabIndex = 12;
            lblStatus.Text = "이곳에 프로그램의 상태가 표시됩니다.";
            // 
            // lvResults
            // 
            lvResults.Location = new Point(99, 319);
            lvResults.Name = "lvResults";
            lvResults.Size = new Size(541, 177);
            lvResults.TabIndex = 13;
            lvResults.UseCompatibleStateImageBehavior = false;
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
            // btnHistory
            // 
            btnHistory.Location = new Point(565, 236);
            btnHistory.Name = "btnHistory";
            btnHistory.Size = new Size(75, 23);
            btnHistory.TabIndex = 21;
            btnHistory.Text = "작업 이력";
            btnHistory.UseVisualStyleBackColor = true;
            // 
            // btnAbout
            // 
            btnAbout.Location = new Point(12, 12);
            btnAbout.Name = "btnAbout";
            btnAbout.Size = new Size(75, 23);
            btnAbout.TabIndex = 23;
            btnAbout.Text = "정보";
            btnAbout.UseVisualStyleBackColor = true;
            // 
            // lvJobs
            // 
            lvJobs.Location = new Point(99, 52);
            lvJobs.Name = "lvJobs";
            lvJobs.Size = new Size(541, 149);
            lvJobs.TabIndex = 24;
            lvJobs.UseCompatibleStateImageBehavior = false;
            // 
            // btnAddJob
            // 
            btnAddJob.Location = new Point(322, 207);
            btnAddJob.Name = "btnAddJob";
            btnAddJob.Size = new Size(75, 23);
            btnAddJob.TabIndex = 25;
            btnAddJob.Text = "추가";
            btnAddJob.UseVisualStyleBackColor = true;
            // 
            // btnEditJob
            // 
            btnEditJob.Location = new Point(403, 207);
            btnEditJob.Name = "btnEditJob";
            btnEditJob.Size = new Size(75, 23);
            btnEditJob.TabIndex = 26;
            btnEditJob.Text = "편집";
            btnEditJob.UseVisualStyleBackColor = true;
            // 
            // btnRemoveJob
            // 
            btnRemoveJob.Location = new Point(484, 207);
            btnRemoveJob.Name = "btnRemoveJob";
            btnRemoveJob.Size = new Size(75, 23);
            btnRemoveJob.TabIndex = 27;
            btnRemoveJob.Text = "삭제";
            btnRemoveJob.UseVisualStyleBackColor = true;
            // 
            // btnRunJob
            // 
            btnRunJob.Location = new Point(565, 207);
            btnRunJob.Name = "btnRunJob";
            btnRunJob.Size = new Size(75, 23);
            btnRunJob.TabIndex = 28;
            btnRunJob.Text = "지금 실행";
            btnRunJob.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(739, 512);
            Controls.Add(btnRunJob);
            Controls.Add(btnRemoveJob);
            Controls.Add(btnEditJob);
            Controls.Add(btnAddJob);
            Controls.Add(lvJobs);
            Controls.Add(btnAbout);
            Controls.Add(btnHistory);
            Controls.Add(chkAutoStart);
            Controls.Add(lvResults);
            Controls.Add(lblStatus);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Log Rogue";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Label lblStatus;
        private ListView lvResults;
        private CheckBox chkAutoStart;
        private Button btnHistory;
        private Button btnAbout;
        private ListView lvJobs;
        private Button btnAddJob;
        private Button btnEditJob;
        private Button btnRemoveJob;
        private Button btnRunJob;
    }
}
