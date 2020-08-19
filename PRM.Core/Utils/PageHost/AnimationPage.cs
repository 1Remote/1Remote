using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Shawn.Utils.PageHost
{
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
