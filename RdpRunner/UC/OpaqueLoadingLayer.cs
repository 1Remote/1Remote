using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace OpaqueLayer
{
    [ToolboxBitmap(typeof(OpaqueLoadingLayer))]
    public class OpaqueLoadingLayer : System.Windows.Forms.Control
    {
        /// <summary>
        /// opacity 0 - 1
        /// </summary>
        private readonly double _opacity;

        public OpaqueLoadingLayer() : this(0.5, Color.White)
        {

        }

        public OpaqueLoadingLayer(double opacity, Color bgColor)
        {
            this._opacity = opacity;
            SetStyle(System.Windows.Forms.ControlStyles.Opaque, true);
            base.CreateControl();

            PictureBox loading = new PictureBox();
            loading.BackColor = bgColor;
            loading.Image = RdpRunner.Properties.Resources.loading;
            loading.Name = "loading";
            loading.Size = new System.Drawing.Size(64, 64);
            loading.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            Point Location = new Point(this.Location.X + (this.Width - loading.Width) / 2, this.Location.Y + (this.Height - loading.Height) / 2);//居中
            loading.Location = Location;
            loading.Anchor = AnchorStyles.None;
            this.Controls.Add(loading);
        }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            var drawColor = Color.FromArgb((int)(this._opacity * 255), this.BackColor);
            var labelBorderPen = new Pen(drawColor, 0);
            var labelBackColorBrush = new SolidBrush(drawColor);

            float controlWidth = this.Size.Width;
            float controlHeight = this.Size.Height;
            e.Graphics.DrawRectangle(labelBorderPen, 0, 0, controlWidth, controlHeight);
            e.Graphics.FillRectangle(labelBackColorBrush, 0, 0, controlWidth, controlHeight);

            base.OnPaint(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; //0x20;  WS_EX_TRANSPARENT, enable  TRANSPARENT
                return cp;
            }
        }
    }


}
