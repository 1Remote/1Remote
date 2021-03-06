using System;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Shawn.Utils
{
    /// <summary>
    /// Mouse click event to command by attach property
    /// in wpf: tabWindow:MouseMiddleClick.MouseMiddleDown="{Bind YourCmd}"
    /// ref: https://stackoverflow.com/questions/20288715/wpf-handle-mousedown-events-from-within-a-datatemplate
    /// </summary>
    public class MouseMiddleClick
    {
        public static readonly DependencyProperty MouseMiddleDownProperty =
            DependencyProperty.RegisterAttached("MouseMiddleDown", typeof(ICommand), typeof(MouseMiddleClick),
                new FrameworkPropertyMetadata(MouseMiddleDownPropertySetCallBack));

        public static void SetMouseMiddleDown(DependencyObject sender, ICommand value)
        {
            sender.SetValue(MouseMiddleDownProperty, value);
        }

        public static ICommand GetMouseMiddleDown(DependencyObject sender)
        {
            return sender.GetValue(MouseMiddleDownProperty) as ICommand;
        }

        private static void MouseMiddleDownPropertySetCallBack(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is UIElement element)
            {
                if (e.OldValue != null)
                {
                    element.RemoveHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(Handler));
                }
                if (e.NewValue != null)
                {
                    element.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(Handler), true);
                }
            }
        }

        private static void Handler(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton != MouseButtonState.Pressed) return;
            var element = sender as UIElement;
            if (element?.GetValue(MouseMiddleDownProperty) is ICommand cmd)
            {
                if (cmd is RoutedCommand routedCmd)
                {
                    if (routedCmd.CanExecute(element, element))
                    {
                        routedCmd.Execute(element, element);
                    }
                }
                else
                {
                    if (cmd.CanExecute(element))
                    {
                        cmd.Execute(element);
                    }
                }
            }
        }
    }
}
