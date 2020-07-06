using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PRM.Core.Resources.Controls
{
    public class ButtonEx : Button
    {
        static ButtonEx()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonEx), new FrameworkPropertyMetadata(typeof(ButtonEx)));//使KButton去读取KButton类型的样式，而不是去读取Button的样式
        }


        #region ForegroundOnMouseOver
        public static readonly DependencyProperty ForegroundOnMouseOverProperty = DependencyProperty.Register("ForegroundOnMouseOver", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Black));
        public Brush ForegroundOnMouseOver
        {
            get
            {
                var v = GetValue(ForegroundOnMouseOverProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnMouseOverProperty, value);
        }
        #endregion

        #region ForegroundOnPressed
        public static readonly DependencyProperty ForegroundOnPressedProperty = DependencyProperty.Register("ForegroundOnPressed", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Black));
        public Brush ForegroundOnPressed
        {
            get
            {
                var v = GetValue(ForegroundOnPressedProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnPressedProperty, value);
        }
        #endregion

        #region ForegroundOnDisabled
        public static readonly DependencyProperty ForegroundOnDisabledProperty = DependencyProperty.Register("ForegroundOnDisabled", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.DarkGray));
        public Brush ForegroundOnDisabled
        {
            get
            {
                var v = GetValue(ForegroundOnDisabledProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnDisabledProperty, value);
        }
        #endregion


        #region BackgroundOnMouseOver
        public static readonly DependencyProperty BackgroundOnMouseOverProperty = DependencyProperty.Register("BackgroundOnMouseOver", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Aqua));
        public Brush BackgroundOnMouseOver
        {
            get
            {
                var v = GetValue(BackgroundOnMouseOverProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnMouseOverProperty, value);
        }
        #endregion

        #region BackgroundOnPressed
        public static readonly DependencyProperty BackgroundOnPressedProperty = DependencyProperty.Register("BackgroundOnPressed", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Aquamarine));
        public Brush BackgroundOnPressed
        {
            get
            {
                var v = GetValue(BackgroundOnPressedProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnPressedProperty, value);
        }
        #endregion

        #region BackgroundOnDisabled
        public static readonly DependencyProperty BackgroundOnDisabledProperty = DependencyProperty.Register("BackgroundOnDisabled", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Gray));
        public Brush BackgroundOnDisabled
        {
            get
            {
                var v = GetValue(BackgroundOnDisabledProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnDisabledProperty, value);
        }
        #endregion


        #region OpacityOnMouseOver
        public static readonly DependencyProperty OpacityOnMouseOverProperty = DependencyProperty.Register("OpacityOnMouseOver", typeof(double), typeof(ButtonEx), new PropertyMetadata(1.0));
        public double OpacityOnMouseOver
        {
            get
            {
                var v = GetValue(OpacityOnMouseOverProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnMouseOverProperty, value);
        }
        #endregion

        #region OpacityOnPressed
        public static readonly DependencyProperty OpacityOnPressedProperty = DependencyProperty.Register("OpacityOnPressed", typeof(double), typeof(ButtonEx), new PropertyMetadata(0.5));
        public double OpacityOnPressed
        {
            get
            {
                var v = GetValue(OpacityOnPressedProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnPressedProperty, value);
        }
        #endregion

        #region OpacityOnDisabled
        public static readonly DependencyProperty OpacityOnDisabledProperty = DependencyProperty.Register("OpacityOnDisabled", typeof(double), typeof(ButtonEx), new PropertyMetadata(1.0));
        public double OpacityOnDisabled
        {
            get
            {
                var v = GetValue(OpacityOnDisabledProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnDisabledProperty, value);
        }
        #endregion


        #region BorderCornerRadius
        public static readonly DependencyProperty BorderCornerRadiusProperty = DependencyProperty.Register("BorderCornerRadius", typeof(CornerRadius), typeof(ButtonEx), new PropertyMetadata(new CornerRadius(0, 0, 0, 0)));
        public CornerRadius BorderCornerRadius
        {
            get
            {
                var v = GetValue(BorderCornerRadiusProperty);
                return (CornerRadius)v;
            }
            set => SetValue(BorderCornerRadiusProperty, value);
        }
        #endregion
    }
}
