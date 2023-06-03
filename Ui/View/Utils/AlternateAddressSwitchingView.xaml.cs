using Stylet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _1RM.Utils;
using Shawn.Utils.Wpf;
using _1RM.Service;
using Shawn.Utils.WpfResources.Theme.Styles;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace _1RM.View.Utils
{
    public partial class AlternateAddressSwitchingView : WindowChromeBase
    {
        public double TopFrom
        {
            get; set;
        }
        private static readonly HashSet<AlternateAddressSwitchingView> _dialogs = new HashSet<AlternateAddressSwitchingView>();
        public AlternateAddressSwitchingView()
        {
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _dialogs.Remove(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TopFrom = GetTopFrom();
            Top = TopFrom - ActualHeight;
            Left = SystemParameters.WorkArea.Right - ActualWidth;
            _dialogs.Add(this);
        }


        private double GetTopFrom()
        {
            //屏幕的高度-底部TaskBar的高度。
            double topFrom = System.Windows.SystemParameters.WorkArea.Bottom;
            bool isContinueFind = _dialogs.Any(o => Math.Abs(o.TopFrom - topFrom) < 0.001);

            while (isContinueFind)
            {
                topFrom -= this.ActualHeight;
                isContinueFind = _dialogs.Any(o => Math.Abs(o.TopFrom - topFrom) < 0.001);
            }

            if (topFrom <= 0)
                topFrom = System.Windows.SystemParameters.WorkArea.Bottom - 10;

            return topFrom;
        }

        private void ButtonClose_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is AlternateAddressSwitchingViewModel vm)
            {
                vm.CmdCloseContinue.Execute(null);
            }

            //this.Dispatcher.Invoke(delegate
            //{
            //    double right = SystemParameters.WorkArea.Right;
            //    DoubleAnimation animation = new DoubleAnimation();
            //    animation.Duration = new Duration(TimeSpan.FromMilliseconds(500));
            //    animation.Completed += (s, a) => { this.Close(); };
            //    animation.From = right - this.ActualWidth;
            //    animation.To = right;
            //    animation.Completed += (o, args) =>
            //    {
            //        if (this.DataContext is AlternateAddressSwitchingViewModel vm)
            //        {
            //            vm.CmdCloseContinue.Execute(null);
            //        }
            //    };
            //    this.BeginAnimation(Window.LeftProperty, animation);
            //});
        }
    }
}
