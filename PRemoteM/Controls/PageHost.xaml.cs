using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using Shawn.Ulits;

namespace Shawn.Ulits.PageHost
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
                int w = (int) (this.ActualWidth * 1.2);
                int h = (int) (this.ActualHeight * 1.2);


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

                    _animationNewPageUnload = AnimationPage.GetInOutStoryboard(1, _oldPage.OutAnimationType, w, h);
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
                    _animationNewPageOnload = AnimationPage.GetInOutStoryboard(1, _newPage.InAnimationType, w, h);
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



    public class AnimationPage
    {
        public InOutAnimationType InAnimationType = InOutAnimationType.None;
        public InOutAnimationType OutAnimationType = InOutAnimationType.None;
        public UserControl Page = null;







        public enum InOutAnimationType
        {
            None,
            SlideFromRight,
            SlideToRight,
            SlideFromLeft,
            SlideToLeft,
            SlideFromTop,
            SlideToTop,
            SlideFromBottom,
            SlideToBottom,
            FadeIn,
            FadeOut
        }

        public static Storyboard GetInOutStoryboard(double seconds,
            InOutAnimationType animationType,
            double parentWidth, double parentHeight,
            float decelerationRatio = 0.9f)
        {
            var storyboard = new Storyboard();
            var from = new Thickness(0);
            var to = new Thickness(0);
            switch (animationType)
            {
                case InOutAnimationType.None:
                    return null;
                case InOutAnimationType.SlideFromRight:
                case InOutAnimationType.SlideToRight:
                case InOutAnimationType.SlideFromLeft:
                case InOutAnimationType.SlideToLeft:
                case InOutAnimationType.SlideFromTop:
                case InOutAnimationType.SlideToTop:
                case InOutAnimationType.SlideFromBottom:
                case InOutAnimationType.SlideToBottom:
                    switch (animationType)
                    {
                        case InOutAnimationType.SlideFromLeft:
                            from = new Thickness(-parentWidth, 0, parentWidth, 0);
                            break;
                        case InOutAnimationType.SlideFromRight:
                            from = new Thickness(parentWidth, 0, -parentWidth, 0);
                            break;
                        case InOutAnimationType.SlideFromTop:
                            from = new Thickness(0, -parentHeight, 0, parentHeight);
                            break;
                        case InOutAnimationType.SlideFromBottom:
                            from = new Thickness(0, parentHeight, 0, -parentHeight);
                            break;
                        case InOutAnimationType.SlideToLeft:
                            to = new Thickness(-parentWidth, 0, parentWidth, 0);
                            break;
                        case InOutAnimationType.SlideToRight:
                            to = new Thickness(parentWidth, 0, -parentWidth, 0);
                            break;
                        case InOutAnimationType.SlideToTop:
                            to = new Thickness(0, -parentHeight, 0, parentHeight);
                            break;
                        case InOutAnimationType.SlideToBottom:
                            to = new Thickness(0, parentHeight, 0, -parentHeight);
                            break;
                    }
                    StoryboardHelpers.AddThicknessAnimation(storyboard, seconds, from, to, "Margin", decelerationRatio);
                    break;
                case InOutAnimationType.FadeIn:
                    StoryboardHelpers.AddFadeIn(storyboard, seconds);
                    break;
                case InOutAnimationType.FadeOut:
                    StoryboardHelpers.AddFadeOut(storyboard, seconds);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(animationType), animationType, null);
            }
            return storyboard;
        }
    }
}
