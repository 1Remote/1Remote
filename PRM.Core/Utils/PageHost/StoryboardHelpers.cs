using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Shawn.Utils.PageHost
{
    public static class StoryboardHelpers
    {
        public static void AddSlideFromRight(this Storyboard storyboard, double seconds, double parentWidth,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(parentWidth, 0, -parentWidth, 0);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideFromLeft(this Storyboard storyboard, double seconds, double parentWidth,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(-parentWidth, 0, parentWidth, 0);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideToRight(this Storyboard storyboard, double seconds, double parentWidth,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(parentWidth, 0, -parentWidth, 0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideToLeft(this Storyboard storyboard, double seconds, double parentWidth,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(-parentWidth, 0, parentWidth, 0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideFromTop(this Storyboard storyboard, double seconds, double parentHeight,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0, -parentHeight, 0, parentHeight);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideFromBottom(this Storyboard storyboard, double seconds, double parentHeight,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0, parentHeight, 0, -parentHeight);
            var to = new Thickness(0);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideToTop(this Storyboard storyboard, double seconds, double parentHeight,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(0, -parentHeight, 0, parentHeight);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddSlideToBottom(this Storyboard storyboard, double seconds, double parentHeight,
            float decelerationRatio = 0.9f)
        {
            var from = new Thickness(0);
            var to = new Thickness(0, parentHeight, 0, -parentHeight);
            storyboard.AddThicknessAnimation(seconds, from, to, "Margin", decelerationRatio);
        }

        public static void AddFadeIn(this Storyboard storyboard, double seconds)
        {
            AddDoubleAnimation(storyboard, seconds, 0, 1, "Opacity");
        }

        public static void AddFadeOut(this Storyboard storyboard, double seconds)
        {
            AddDoubleAnimation(storyboard, seconds, 1, 0, "Opacity");
        }

        public static void AddDoubleAnimation(this Storyboard storyboard, double seconds,
            double from, double to,
            string propertyName,
            float decelerationRatio = 0.9f)
        {
            var animation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                From = from,
                To = to,
            };
            Storyboard.SetTargetProperty(animation, new PropertyPath(propertyName));
            storyboard.Children.Add(animation);
        }

        /// <summary>
        /// 为 storyboard 添加一个针对 Margin 属性的线性插值动画
        /// </summary>
        /// <param name="storyboard"></param>
        /// <param name="seconds"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="propertyName">动画将被应用到哪个属性</param>
        /// <param name="decelerationRatio"></param>
        public static void AddThicknessAnimation(this Storyboard storyboard, double seconds,
            Thickness from, Thickness to,
            string propertyName,
            float decelerationRatio = 0.9f)
        {
            var animation = new ThicknessAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                From = from,
                To = to,
                DecelerationRatio = decelerationRatio
            };
            Storyboard.SetTargetProperty(animation, new PropertyPath(propertyName));
            storyboard.Children.Add(animation);
        }
    }
}