using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Shawn.Utils.PageHost
{
    public partial class PageHost : UserControl
    {
        public static readonly DependencyProperty NewPageProperty = DependencyProperty.Register("NewPage",
            typeof(AnimationPage),
            typeof(PageHost),
            new PropertyMetadata(null, new PropertyChangedCallback(OnNewPagePropertyChanged)));

        private static void OnNewPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PageHost)d).Show(e.NewValue as AnimationPage);
        }

        public AnimationPage NewPage
        {
            get => (AnimationPage)GetValue(NewPageProperty);
            set => SetValue(NewPageProperty, value);
        }

        public PageHost()
        {
            InitializeComponent();
        }

        private AnimationPage _oldPage = null;
        private AnimationPage _newPage = null;
        private Storyboard _animationNewPageOnload = null;
        private Storyboard _animationNewPageUnload = null;

        public void Show(AnimationPage newPage)
        {
            _oldPage = _newPage;
            _newPage = newPage;

            Dispatcher.Invoke((Action)delegate
            {
                NewPageHost.Content = null;
                OldPageHost.Content = null;
                int w = (int)(this.ActualWidth * 1.2);
                int h = (int)(this.ActualHeight * 1.2);

                if (_oldPage?.Page != null)
                {
                    try
                    {
                        _oldPage.Page.Loaded -= PageLoaded;
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    _animationNewPageUnload = _oldPage.GetOutAnimationStoryboard(w, h);
                    if (_animationNewPageUnload != null)
                    {
                        OldPageHost.Content = _oldPage.Page;
                        _animationNewPageUnload.Completed += PageOnNewPageUnload;
                        OldPageHost.BeginAnimation(ContentControl.MarginProperty, null);
                        OldPageHost.BeginAnimation(ContentControl.OpacityProperty, null);
                        _animationNewPageUnload.Begin(OldPageHost);
                    }
                }

                if (_newPage?.Page != null)
                {
                    _animationNewPageOnload = _newPage.GetInAnimationStoryboard(w, h);
                    _newPage.Page.Loaded += PageLoaded;
                    NewPageHost.Content = _newPage.Page;
                }
            });
        }

        private void PageOnNewPageUnload(object sender, EventArgs e)
        {
            OldPageHost.Content = null;
            _animationNewPageUnload.Completed -= PageOnNewPageUnload;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            _animationNewPageOnload?.Begin(NewPageHost);
        }
    }
}