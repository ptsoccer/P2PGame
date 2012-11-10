namespace P2PGame
{
    partial class P2PClient
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
            this.txtGameChat = new System.Windows.Forms.RichTextBox();
            this.lstPeers = new System.Windows.Forms.ListView();
            this.name = new System.Windows.Forms.ColumnHeader();
            this.ping = new System.Windows.Forms.ColumnHeader();
            this.tmrPollMessages = new System.Windows.Forms.Timer(this.components);
            this.txtMessage = new System.Windows.Forms.RichTextBox();
            this.btnKick = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // txtGameChat
            // 
            this.txtGameChat.BackColor = System.Drawing.Color.White;
            this.txtGameChat.DetectUrls = false;
            this.txtGameChat.Location = new System.Drawing.Point(12, 12);
            this.txtGameChat.Name = "txtGameChat";
            this.txtGameChat.ReadOnly = true;
            this.txtGameChat.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtGameChat.Size = new System.Drawing.Size(436, 246);
            this.txtGameChat.TabIndex = 1;
            this.txtGameChat.TabStop = false;
            this.txtGameChat.Text = "";
            // 
            // lstPeers
            // 
            this.lstPeers.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.name,
            this.ping});
            this.lstPeers.FullRowSelect = true;
            this.lstPeers.Location = new System.Drawing.Point(454, 12);
            this.lstPeers.MultiSelect = false;
            this.lstPeers.Name = "lstPeers";
            this.lstPeers.Size = new System.Drawing.Size(169, 133);
            this.lstPeers.TabIndex = 2;
            this.lstPeers.TabStop = false;
            this.lstPeers.UseCompatibleStateImageBehavior = false;
            this.lstPeers.View = System.Windows.Forms.View.Details;
            // 
            // name
            // 
            this.name.Text = "Name";
            this.name.Width = 100;
            // 
            // ping
            // 
            this.ping.Text = "Ping";
            this.ping.Width = 67;
            // 
            // tmrPollMessages
            // 
            this.tmrPollMessages.Enabled = true;
            this.tmrPollMessages.Interval = 1;
            this.tmrPollMessages.Tick += new System.EventHandler(this.tmrPollMessages_Tick);
            // 
            // txtMessage
            // 
            this.txtMessage.Location = new System.Drawing.Point(14, 269);
            this.txtMessage.Multiline = false;
            this.txtMessage.Name = "txtMessage";
            this.txtMessage.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.txtMessage.Size = new System.Drawing.Size(434, 25);
            this.txtMessage.TabIndex = 0;
            this.txtMessage.Text = "";
            this.txtMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtMessage_KeyDown);
            // 
            // btnKick
            // 
            this.btnKick.Location = new System.Drawing.Point(458, 164);
            this.btnKick.Name = "btnKick";
            this.btnKick.Size = new System.Drawing.Size(164, 35);
            this.btnKick.TabIndex = 3;
            this.btnKick.Text = "Kick";
            this.btnKick.UseVisualStyleBackColor = true;
            this.btnKick.Click += new System.EventHandler(this.btnKick_Click);
            // 
            // P2PClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(635, 327);
            this.Controls.Add(this.btnKick);
            this.Controls.Add(this.txtMessage);
            this.Controls.Add(this.lstPeers);
            this.Controls.Add(this.txtGameChat);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "P2PClient";
            this.Text = "P2PClient";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox txtGameChat;
        private System.Windows.Forms.ListView lstPeers;
        private System.Windows.Forms.ColumnHeader name;
        private System.Windows.Forms.ColumnHeader ping;
        private System.Windows.Forms.Timer tmrPollMessages;
        private System.Windows.Forms.RichTextBox txtMessage;
        private System.Windows.Forms.Button btnKick;

    }
}