using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.ViewModel;

namespace PRM.Controls
{
    /// <summary>
    /// ServerCard.xaml 的交互逻辑
    /// </summary>
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty VmServerCardProperty =
            DependencyProperty.Register("VmServerCard", typeof(VmServerCard), typeof(ServerCard),
                new PropertyMetadata(new VmServerCard(), new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            VmServerCard value = (VmServerCard)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }

        public VmServerCard VmServerCard
        {
            get { return (VmServerCard)GetValue(VmServerCardProperty); }
            set { SetValue(VmServerCardProperty, value); }
        }

        public ServerCard()
        {
            InitializeComponent();
        }

        private void Card_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
            {
                (this.DataContext as VmServerCard)?.Server?.Conn();
            }
        }

        private void BtnSetting_OnClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
