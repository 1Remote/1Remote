namespace RdpRunner
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.axMsRdpClient11NotSafeForScripting1 = new AxMSTSCLib.AxMsRdpClient11NotSafeForScripting();
            this.rdp = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
            ((System.ComponentModel.ISupportInitialize)(this.axMsRdpClient11NotSafeForScripting1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.rdp)).BeginInit();
            this.SuspendLayout();
            // 
            // axMsRdpClient11NotSafeForScripting1
            // 
            this.axMsRdpClient11NotSafeForScripting1.Enabled = true;
            this.axMsRdpClient11NotSafeForScripting1.Location = new System.Drawing.Point(0, 0);
            this.axMsRdpClient11NotSafeForScripting1.Name = "axMsRdpClient11NotSafeForScripting1";
            this.axMsRdpClient11NotSafeForScripting1.TabIndex = 0;
            // 
            // rdp
            // 
            this.rdp.Enabled = true;
            this.rdp.Location = new System.Drawing.Point(0, 0);
            this.rdp.Name = "rdp";
            this.rdp.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("rdp.OcxState")));
            this.rdp.Size = new System.Drawing.Size(1006, 838);
            this.rdp.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(192F, 192F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1030, 938);
            this.Controls.Add(this.rdp);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.axMsRdpClient11NotSafeForScripting1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.rdp)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private AxMSTSCLib.AxMsRdpClient11NotSafeForScripting axMsRdpClient11NotSafeForScripting1;
        private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting rdp;
    }
}

