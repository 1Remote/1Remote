using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using CMControls.RoundButtons;
using Stylet;

namespace _1RM.View.Host.ProtocolHosts
{
    public partial class WinformMaskLayer : UserControl
    {
        readonly FlowLayoutPanel _labelPanel = new FlowLayoutPanel();
        readonly Label _labelTitle = new Label();
        readonly Label _labelMessage = new Label();
        readonly FlowLayoutPanel _buttonPanel = new FlowLayoutPanel();
        readonly CircleAnimationPanel _animationPanel = new CircleAnimationPanel();
        readonly Button _reconn;
        readonly Button _dismiss;

        public Action? ReconnectOnClick;
        public Action? DismissOnClick;

        public WinformMaskLayer()
        {
            InitializeComponent();

            BackColor = Color.Black;
            Dock = DockStyle.Fill;

            //#if DEV_RDP
            //            BackColor = Color.Gray;
            //#endif
            ForeColor = Color.White;

            DoubleBuffered = true;

            var flowLayoutPanel1 = new FlowLayoutPanel();
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.AutoSize = true;
            //flowLayoutPanel1.Width = 600;
            //flowLayoutPanel1.Height = 600;
            flowLayoutPanel1.BackColor = Color.Black;
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.WrapContents = false;
            //flowLayoutPanel1.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            flowLayoutPanel1.Anchor = AnchorStyles.None;

            // ring
            {
                _animationPanel.Enabled = false;
                _animationPanel.Visible = false;
                flowLayoutPanel1.Controls.Add(_animationPanel);
            }

            // message
            {
                _labelTitle.AutoSize = false;
                _labelTitle.Width = 500;
                _labelTitle.Height = 100;
                _labelTitle.TextAlign = ContentAlignment.BottomCenter;
                _labelTitle.Text = "title";
                _labelTitle.Font = new Font(_labelTitle.Font.FontFamily, 18);
                _labelTitle.Margin = new Padding(0, 0, 0, 20);

                _labelMessage.AutoSize = false;
                _labelMessage.Font = new Font(_labelMessage.Font.FontFamily, 12);
                _labelMessage.TextAlign = ContentAlignment.MiddleCenter;
                _labelMessage.Width = 500;
                _labelMessage.Height = 150;
                _labelMessage.Text = "message";

                _labelPanel.FlowDirection = FlowDirection.TopDown;
                _labelPanel.Width = 500;
                _labelPanel.Height = 270;
                _labelPanel.BackColor = Color.Black;
                _labelPanel.Dock = DockStyle.None;
                _labelPanel.Anchor = AnchorStyles.None;
                _labelPanel.Margin = new Padding(0, 0, 0, 20);
                _labelPanel.Controls.Add(_labelTitle);
                _labelPanel.Controls.Add(_labelMessage);

                _labelPanel.Enabled = false;
                _labelPanel.Visible = false;
                flowLayoutPanel1.Controls.Add(_labelPanel);
            }


            // buttons
            {
                _reconn = new RoundButton()
                {
                    Text = "TXT: Reconn",
                    Width = 100,
                    Height = 30,
                    AutoSize = true,
                };
                _reconn.Click += (sender, args) =>
                {
                    ReconnectOnClick?.Invoke();
                };

                _dismiss = new RoundButton()
                {
                    Text = "TXT: Dismiss",
                    Width = 100,
                    Height = 30,
                    AutoSize = true,
                };
                _dismiss.Click += (sender, args) =>
                {
                    DismissOnClick?.Invoke();
                };


                _buttonPanel.FlowDirection = FlowDirection.LeftToRight;
                _buttonPanel.AutoSize = true;
                _buttonPanel.BackColor = Color.Black;
                _buttonPanel.Dock = DockStyle.None;
                _buttonPanel.Anchor = AnchorStyles.None;
                _buttonPanel.Controls.Add(_reconn);
                _buttonPanel.Controls.Add(_dismiss);
                _buttonPanel.Enabled = false;
                _buttonPanel.Visible = false;
                flowLayoutPanel1.Controls.Add(_buttonPanel);
            }



            // 居中显示
            var tableLayoutPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(flowLayoutPanel1, 0, 0);

            Controls.Add(tableLayoutPanel);


            //this.SetStyle(
            //    ControlStyles.UserPaint |  //控件自行绘制，而不使用操作系统的绘制
            //    ControlStyles.AllPaintingInWmPaint | //忽略背景擦除的Windows消息，减少闪烁，只有UserPaint设为true时才能使用。
            //    ControlStyles.OptimizedDoubleBuffer |//在缓冲区上绘制，不直接绘制到屏幕上，减少闪烁。
            //    ControlStyles.ResizeRedraw | //控件大小发生变化时，重绘。                  
            //    ControlStyles.SupportsTransparentBackColor, //支持透明背景颜色
            //    true);

            ShowProcessingRing();
            //ShowMessage("123123123", "45345345345");

            Invalidate();
        }

        public void Set(string value)
        {
            Execute.OnUIThread(() =>
            {
                _labelMessage.Text = value;
            });
        }

        public new void Hide()
        {
            Execute.OnUIThreadSync(() =>
            {
                base.Hide();
                Enabled = false;
            });
        }

        private new void Show()
        {
            base.Show();
            if (!Enabled)
                Enabled = true;
            if (!Visible)
                Visible = true;
        }

        public void ShowProcessingRing()
        {
            Execute.OnUIThreadSync(() =>
            {
                Show();
                _labelPanel.Enabled = false;
                _labelPanel.Visible = false;
                _buttonPanel.Enabled = false;
                _buttonPanel.Visible = false;
                _animationPanel.Enabled = true;
                _animationPanel.Visible = true;
            });
        }

        public void ShowMessage(string message, string title = "", bool reconnectButton = true, bool dismissButton = true)
        {
            Execute.OnUIThreadSync(() =>
            {
                Show();
                _reconn.Enabled = reconnectButton;
                _reconn.Visible = reconnectButton;
                _dismiss.Enabled = dismissButton;
                _dismiss.Visible = dismissButton;

                _labelMessage.Text = message;
                _labelTitle.Visible = !string.IsNullOrEmpty(title);
                _labelTitle.Text = title;

                _labelPanel.Enabled = true;
                _labelPanel.Visible = true;
                _buttonPanel.Enabled = true;
                _buttonPanel.Visible = true;
                _animationPanel.Enabled = false;
                _animationPanel.Visible = false;
            });
        }
    }
}
