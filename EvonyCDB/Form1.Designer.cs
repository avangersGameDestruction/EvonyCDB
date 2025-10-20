namespace EvonyCDB
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
            groupBox1 = new GroupBox();
            lblStatus = new Label();
            btnConnect = new Button();
            comboBox1 = new ComboBox();
            groupBox2 = new GroupBox();
            label1 = new Label();
            CoordsRichTextBox = new RichTextBox();
            groupBox3 = new GroupBox();
            label2 = new Label();
            checkedListBox1 = new CheckedListBox();
            progressBar1 = new ProgressBar();
            btnActivate = new Button();
            ActivityLogRichTextBox = new RichTextBox();
            groupBox4 = new GroupBox();
            SortMonstersrichtextbox = new RichTextBox();
            clearbtn = new Button();
            Sortbtn = new Button();
            comboBox2 = new ComboBox();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            groupBox4.SuspendLayout();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(lblStatus);
            groupBox1.Controls.Add(btnConnect);
            groupBox1.Controls.Add(comboBox1);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(523, 69);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "PID Finder";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(337, 26);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 15);
            lblStatus.TabIndex = 2;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(223, 22);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(108, 23);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Scan";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += button1_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Evony.exe" });
            comboBox1.Location = new Point(6, 22);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(211, 23);
            comboBox1.TabIndex = 0;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(CoordsRichTextBox);
            groupBox2.Location = new Point(12, 87);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(261, 299);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "Monster coords";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 28);
            label1.Name = "label1";
            label1.Size = new Size(216, 15);
            label1.TabIndex = 1;
            label1.Text = "just copy paste the coords from discord";
            // 
            // CoordsRichTextBox
            // 
            CoordsRichTextBox.Location = new Point(6, 46);
            CoordsRichTextBox.Name = "CoordsRichTextBox";
            CoordsRichTextBox.Size = new Size(249, 238);
            CoordsRichTextBox.TabIndex = 0;
            CoordsRichTextBox.Text = "";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(label2);
            groupBox3.Controls.Add(checkedListBox1);
            groupBox3.Location = new Point(274, 87);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(261, 299);
            groupBox3.TabIndex = 2;
            groupBox3.TabStop = false;
            groupBox3.Text = "Ignore Preset";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 28);
            label2.Name = "label2";
            label2.Size = new Size(223, 15);
            label2.TabIndex = 1;
            label2.Text = "Selected bosses in this list will be ignored";
            // 
            // checkedListBox1
            // 
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(5, 46);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(249, 238);
            checkedListBox1.TabIndex = 0;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(126, 460);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(409, 23);
            progressBar1.TabIndex = 3;
            // 
            // btnActivate
            // 
            btnActivate.Location = new Point(12, 460);
            btnActivate.Name = "btnActivate";
            btnActivate.Size = new Size(108, 23);
            btnActivate.TabIndex = 4;
            btnActivate.Text = "Activate";
            btnActivate.UseVisualStyleBackColor = true;
            btnActivate.Click += btnActivate_Click;
            // 
            // ActivityLogRichTextBox
            // 
            ActivityLogRichTextBox.Location = new Point(12, 387);
            ActivityLogRichTextBox.Name = "ActivityLogRichTextBox";
            ActivityLogRichTextBox.Size = new Size(523, 67);
            ActivityLogRichTextBox.TabIndex = 5;
            ActivityLogRichTextBox.Text = "";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(SortMonstersrichtextbox);
            groupBox4.Controls.Add(clearbtn);
            groupBox4.Controls.Add(Sortbtn);
            groupBox4.Controls.Add(comboBox2);
            groupBox4.Location = new Point(541, 12);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(319, 471);
            groupBox4.TabIndex = 6;
            groupBox4.TabStop = false;
            groupBox4.Text = "Monster sort system";
            // 
            // SortMonstersrichtextbox
            // 
            SortMonstersrichtextbox.Location = new Point(6, 55);
            SortMonstersrichtextbox.Name = "SortMonstersrichtextbox";
            SortMonstersrichtextbox.Size = new Size(307, 358);
            SortMonstersrichtextbox.TabIndex = 3;
            SortMonstersrichtextbox.Text = "";
            // 
            // clearbtn
            // 
            clearbtn.Location = new Point(6, 442);
            clearbtn.Name = "clearbtn";
            clearbtn.Size = new Size(307, 23);
            clearbtn.TabIndex = 2;
            clearbtn.Text = "Clear";
            clearbtn.UseVisualStyleBackColor = true;
            clearbtn.Click += clearbtn_Click;
            // 
            // Sortbtn
            // 
            Sortbtn.Location = new Point(6, 419);
            Sortbtn.Name = "Sortbtn";
            Sortbtn.Size = new Size(307, 23);
            Sortbtn.TabIndex = 1;
            Sortbtn.Text = "Sort Bosses";
            Sortbtn.UseVisualStyleBackColor = true;
            Sortbtn.Click += Sortbtn_Click;
            // 
            // comboBox2
            // 
            comboBox2.FormattingEnabled = true;
            comboBox2.Items.AddRange(new object[] { "Ammit  ", "Azazel  ", "Kraken  ", "Sphinx  ", "Stymphalian Bird  ", "Warlord" });
            comboBox2.Location = new Point(6, 26);
            comboBox2.Name = "comboBox2";
            comboBox2.Size = new Size(307, 23);
            comboBox2.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(872, 495);
            Controls.Add(groupBox4);
            Controls.Add(ActivityLogRichTextBox);
            Controls.Add(btnActivate);
            Controls.Add(progressBar1);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Evony Discord Coords Bot ";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            groupBox4.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private Label lblStatus;
        private Button btnConnect;
        private ComboBox comboBox1;
        private GroupBox groupBox2;
        private Label label1;
        private RichTextBox CoordsRichTextBox;
        private GroupBox groupBox3;
        private ProgressBar progressBar1;
        private Button btnActivate;
        private CheckedListBox checkedListBox1;
        private Label label2;
        private RichTextBox ActivityLogRichTextBox;
        private GroupBox groupBox4;
        private RichTextBox SortMonstersrichtextbox;
        private Button clearbtn;
        private Button Sortbtn;
        private ComboBox comboBox2;
    }
}
