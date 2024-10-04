using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CMControls.RoundButtons
{
    public sealed class CircleAnimationPanel : Panel, IDisposable
    {
        private int _radius = 10;//半径 
        //private Color _borderColor = Color.FromArgb(51, 161, 224);//边框颜色
        private Color _hoverColor = Color.FromArgb(65, 65, 65);//基颜色
        private Color _normalColor = Color.FromArgb(50, 50, 51);//基颜色
        private Color _pressedColor = Color.FromArgb(45, 45, 45);//基颜色


        private readonly List<Point> pointLst = new List<Point>();    // 偏移点坐标
        private int indexSelect = 0;  // 当前选择索引


        readonly Timer _timer = new Timer();

        public CircleAnimationPanel()
        {

            BackColor = Color.Black;
            //#if DEV_RDP
            //            BackColor = Color.Gray;
            //#endif
            ForeColor = Color.White;

            DoubleBuffered = true;

            Dock = DockStyle.None;
            Anchor = AnchorStyles.None;
            this.Width = 201;
            this.Height = 201;

            this.SetStyle(
             ControlStyles.UserPaint |  //控件自行绘制，而不使用操作系统的绘制
             ControlStyles.AllPaintingInWmPaint | //忽略背景擦除的Windows消息，减少闪烁，只有UserPaint设为true时才能使用。
             ControlStyles.OptimizedDoubleBuffer |//在缓冲区上绘制，不直接绘制到屏幕上，减少闪烁。
             ControlStyles.ResizeRedraw | //控件大小发生变化时，重绘。                  
             ControlStyles.SupportsTransparentBackColor, //支持透明背景颜色
             true);

            this.VisibleChanged += CircleAnimationPanel_VisibleChanged;


            // 圆心坐标
            double centerX = this.Width / 2;
            double centerY = this.Height / 2;
            // 半径
            double radius = 50;
            // 间隔
            double span = Math.PI / 10;
            for (double theta = 0; theta < 2 * Math.PI; theta += span)
            {
                // 计算圆周上的点坐标
                var x = centerX + radius * Math.Cos(theta);
                var y = centerY + radius * Math.Sin(theta);
                //Console.WriteLine("Point: (" + x + ", " + y + ")");
                pointLst.Add(new Point((int)x, (int)y));
            }

            _timer.Interval = 100;
            _timer.Tick += (sender, args) =>
            {
                if (this.Enabled && this.Visible)
                {
                    indexSelect++;
                    if (indexSelect >= pointLst.Count)
                        indexSelect = 0;
                    this.Invalidate();
                }
            };
            _timer.Start();

        }

        ~CircleAnimationPanel()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _timer.Stop();
            _timer.Dispose();
        }

        private void CircleAnimationPanel_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }


        //重写OnPaint
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e);
            // base.OnPaintBackground(e);

            // 尽可能高质量绘制
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;


            using SolidBrush b0 = new SolidBrush(this.BackColor);
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(250, 250, 250));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(200, 200, 200));
            using SolidBrush b3 = new SolidBrush(Color.FromArgb(150, 150, 150));
            using SolidBrush b4 = new SolidBrush(Color.FromArgb(100, 100, 100));
            using SolidBrush b5 = new SolidBrush(Color.FromArgb(50, 50, 50));
            var bs = new List<Brush>() { b1, b2, b3, b4, b5 };

            const int r1 = 3;
            const int d1 = r1 * 2;
            const int r2 = r1 + 2;
            const int d2 = r2 * 2;

            for (int i = 0; i < pointLst.Count; i++)
            {
                e.Graphics.FillEllipse(b0, pointLst[i].X - r2, pointLst[i].Y - r2, d2, d2);
            }

            for (int i = indexSelect; i > indexSelect - bs.Count; i--)
            {
                int j = i;
                if (j < 0)
                    j += pointLst.Count;
                e.Graphics.FillEllipse(bs[indexSelect - i], pointLst[j].X - r1, pointLst[j].Y - r1, d1, d1);
            }
        }
        /// <ummary>
        /// 根据矩形区域rect，计算呈现radius圆角的Graphics路径
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            #region 正确绘制圆角矩形区域
            int R = radius * 2;
            Rectangle arcRect = new Rectangle(rect.Location, new Size(R, R));
            GraphicsPath path = new GraphicsPath();
            // 左上圆弧 左手坐标系，顺时针为正 从180开始，转90度
            path.AddArc(arcRect, 180, 90);
            // 右上圆弧
            arcRect.X = rect.Right - R;
            path.AddArc(arcRect, 270, 90);
            // 右下圆弧
            arcRect.Y = rect.Bottom - R;
            path.AddArc(arcRect, 0, 90);
            // 左下圆弧
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);
            path.CloseFigure();
            return path;
            #endregion
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
        }
    }
}
