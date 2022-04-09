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
using PRM.Service;

namespace PRM.View
{
    /// <summary>
    /// RequestRatingView.xaml 的交互逻辑
    /// </summary>
    public partial class RequestRatingView : UserControl
    {
        public RequestRatingView()
        {
            InitializeComponent();
        }



        private void ButtonDismissEngagementPopup_OnClick(object sender, RoutedEventArgs e)
        {
        }

        private void HyperlinkRating_OnClick(object sender, RoutedEventArgs e)
        {
            this.CbDoNotShowEngagementAgain.IsChecked = true;
        }
    }
}
