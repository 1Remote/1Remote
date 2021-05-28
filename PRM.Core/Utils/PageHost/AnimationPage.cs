using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Shawn.Utils.PageHost
{
    public class AnimationPage
    {
        ~AnimationPage()
        {
            Console.WriteLine($"Release {this.GetType().Name}({this.GetHashCode()})");
        }

        public InOutAnimationType InAnimationType { get; set; } = InOutAnimationType.None;
        public InOutAnimationType OutAnimationType { get; set; } = InOutAnimationType.None;
        public UserControl Page { get; set; } = null;

        private const double AnimationSeconds = 0.3;
        private const float AnimationDecelerationRatio = 0.9f;

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

        public Storyboard GetInAnimationStoryboard(double parentWidth, double parentHeight)
        {
            var storyboard = new Storyboard();
            switch (InAnimationType)
            {
                case InOutAnimationType.SlideFromLeft:
                    storyboard.AddSlideFromLeft(AnimationSeconds, parentWidth, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideFromRight:
                    storyboard.AddSlideFromRight(AnimationSeconds, parentWidth, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideFromTop:
                    storyboard.AddSlideFromTop(AnimationSeconds, parentHeight, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideFromBottom:
                    storyboard.AddSlideFromBottom(AnimationSeconds, parentHeight, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.FadeIn:
                    storyboard.AddFadeIn(AnimationSeconds);
                    break;
            }
            return storyboard;
        }

        public Storyboard GetOutAnimationStoryboard(double parentWidth, double parentHeight)
        {
            var storyboard = new Storyboard();
            switch (OutAnimationType)
            {
                case InOutAnimationType.SlideToLeft:
                    storyboard.AddSlideToLeft(AnimationSeconds, parentWidth, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideToRight:
                    storyboard.AddSlideToRight(AnimationSeconds, parentWidth, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideToTop:
                    storyboard.AddSlideToTop(AnimationSeconds, parentHeight, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.SlideToBottom:
                    storyboard.AddSlideToBottom(AnimationSeconds, parentHeight, AnimationDecelerationRatio);
                    break;

                case InOutAnimationType.FadeOut:
                    storyboard.AddFadeOut(AnimationSeconds);
                    break;
            }
            return storyboard;
        }
    }
}