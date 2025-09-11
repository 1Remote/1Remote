using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _1RM.View.ServerView
{
    /// <summary>
    /// TagsPanelView.xaml 的交互逻辑
    /// </summary>
    public partial class TagsPanelView : UserControl
    {
        public TagsPanelView()
        {
            InitializeComponent();
        }

        private void TagItemSource_OnFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is _1RM.Model.Tag t
                && DataContext is TagsPanelViewModel vm)
            {
                if (string.IsNullOrEmpty(vm.FilterString))
                {
                    e.Accepted = true;
                }
                else if (t.Name.IndexOf(vm.FilterString, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    e.Accepted = true;
                }
                else
                {
                    e.Accepted = false;
                }
            }
        }

        private void TbFilter_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (DataContext is TagsPanelViewModel vm)
            {
                if (e.Key == Key.Escape)
                    vm.FilterString = "";
            }
        }
    }
}
