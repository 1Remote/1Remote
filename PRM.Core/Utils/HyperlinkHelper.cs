using System.Windows;

namespace Shawn.Utils
{
    public static class HyperlinkHelper
    {
        public static bool GetIsOpenExternal(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsOpenExternalProperty);
        }

        public static void SetIsOpenExternal(DependencyObject obj, bool value)
        {
            obj.SetValue(IsOpenExternalProperty, value);
        }

        public static readonly DependencyProperty IsOpenExternalProperty =
            DependencyProperty.RegisterAttached("IsOpenExternal", typeof(bool), typeof(HyperlinkHelper), new UIPropertyMetadata(false, OnIsOpenExternalChanged));

        private static void OnIsOpenExternalChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            System.Windows.Documents.Hyperlink hyperlink = sender as System.Windows.Documents.Hyperlink;

            if ((bool)args.NewValue)
            {
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            }
            else
            {
                hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
            }
        }

        private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}