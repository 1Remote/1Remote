using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Shawn.Utils.WpfResources.Theme.Styles;

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
    }
}
