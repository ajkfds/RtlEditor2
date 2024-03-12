namespace pluginVerilog.Tools
{
    partial class AutoConnectForm
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.colorLabelList = new ajkControls.ColorLabel.ColorLabelList();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(663, 33);
            this.panel1.TabIndex = 1;
            // 
            // colorLabelList
            // 
            this.colorLabelList.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.colorLabelList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.colorLabelList.ForeColor = System.Drawing.Color.Gainsboro;
            this.colorLabelList.Location = new System.Drawing.Point(0, 33);
            this.colorLabelList.Margin = new System.Windows.Forms.Padding(4);
            this.colorLabelList.Name = "colorLabelList";
            this.colorLabelList.Size = new System.Drawing.Size(663, 515);
            this.colorLabelList.TabIndex = 2;
            this.colorLabelList.ColorLabelClicked += new ajkControls.ColorLabel.ColorLabelList.DColorLabelClicked(this.colorLabelList_ColorLabelClicked);
            this.colorLabelList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.colorLabelList_KeyDown);
            // 
            // AutoConnectForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(663, 548);
            this.Controls.Add(this.colorLabelList);
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Consolas", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "AutoConnectForm";
            this.Text = "AutoConnect";
            this.Load += new System.EventHandler(this.AutoConnectForm_Load);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private ajkControls.ColorLabel.ColorLabelList colorLabelList;
    }
}