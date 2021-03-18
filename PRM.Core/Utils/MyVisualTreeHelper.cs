using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shawn.Utils
{
    public class MyVisualTreeHelper
    {
        #region Static Func

        public static T FindAncestor<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T o)
                {
                    return o;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }
            return default(T);
        }

        /// <summary>
        /// 取得指定位置处的 ListViewItem
        /// </summary>
        /// <param name="lvSender"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public static ListViewItem GetItemOnPosition(ScrollContentPresenter lvSender, Point position)
        {
            HitTestResult r = VisualTreeHelper.HitTest(lvSender, position);
            if (r == null)
            {
                return null;
            }
            var obj = r.VisualHit;
            while (!(obj is ListView) && (obj != null))
            {
                obj = VisualTreeHelper.GetParent(obj);
                if (obj is ListViewItem item)
                {
                    return item;
                }
            }
            return null;
        }

        public static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            try
            {
                while (source != null && !(source is T))
                    source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                return source;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        //http://stackoverflow.com/questions/665719/wpf-animate-listbox-scrollviewer-horizontaloffset
        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            // Search immediate children first (breadth-first)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T o)
                {
                    return o;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        #endregion Static Func
    }
}