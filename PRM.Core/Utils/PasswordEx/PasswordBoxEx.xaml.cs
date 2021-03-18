using System.Windows;
using System.Windows.Controls;

namespace Shawn.Utils
{
    public static class PasswordBoxEx
    {
        public static readonly DependencyProperty DependencyEnabledProperty = DependencyProperty.RegisterAttached("DependencyEnabled", typeof(bool),
                                                    typeof(PasswordBoxEx), new PropertyMetadata(false, OnDependencyEnabledChanged));

        private static void OnDependencyEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is PasswordBox passwordBox))
            {
                return;
            }

            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= PasswordChanged;
            }

            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += PasswordChanged;
            }
        }

        public static bool GetDependencyEnabled(DependencyObject dp)
        {
            return (bool)dp.GetValue(DependencyEnabledProperty);
        }

        public static void SetDependencyEnabled(DependencyObject dp, bool value)
        {
            dp.SetValue(DependencyEnabledProperty, value);
        }

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached("Password", typeof(string),
                                                    typeof(PasswordBoxEx), new PropertyMetadata("", OnPasswordPropertyChanged));

        public static string GetPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(PasswordProperty);
        }

        public static void SetPassword(DependencyObject dp, string value)
        {
            dp.SetValue(PasswordProperty, value);
        }

        private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(PasswordBoxEx));

        private static bool GetIsUpdating(DependencyObject dp)
        {
            return (bool)dp.GetValue(IsUpdatingProperty);
        }

        private static void SetIsUpdating(DependencyObject dp, bool value)
        {
            dp.SetValue(IsUpdatingProperty, value);
        }

        private static void OnPasswordPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordChanged;
                if (!(bool)GetIsUpdating(passwordBox))
                {
                    passwordBox.Password = (string)e.NewValue;
                }
                passwordBox.PasswordChanged += PasswordChanged;
            }
        }

        private static void PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetIsUpdating(passwordBox, true);
                SetPassword(passwordBox, passwordBox.Password);
                SetIsUpdating(passwordBox, false);
            }
        }

        public static readonly DependencyProperty PlaceHolderProperty = DependencyProperty.RegisterAttached("PlaceHolder", typeof(string), typeof(PasswordBoxEx), new FrameworkPropertyMetadata(""));

        public static string GetPlaceHolder(DependencyObject d)
        {
            return (string)d.GetValue(PlaceHolderProperty);
        }

        public static void SetPlaceHolder(DependencyObject obj, string value)
        {
            obj.SetValue(PlaceHolderProperty, value);
        }
    }
}