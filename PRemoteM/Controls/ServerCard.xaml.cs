using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.ViewModel;

namespace PRM.Controls
{
    /// <summary>
    /// ServerCard.xaml 的交互逻辑
    /// </summary>
    public partial class ServerCard : UserControl
    {
        public static readonly DependencyProperty VmServerCardProperty =
            DependencyProperty.Register("VmServerCard", typeof(VmServerCard), typeof(ServerCard),
                new PropertyMetadata(new VmServerCard(null, null), new PropertyChangedCallback(OnServerDataChanged)));

        private static void OnServerDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (VmServerCard)e.NewValue;
            ((ServerCard)d).DataContext = value;
        }
        public VmServerCard VmServerCard
        {
            get => (VmServerCard)GetValue(VmServerCardProperty);
            set => SetValue(VmServerCardProperty, value);
        }

        public ServerCard()
        {
            InitializeComponent();
        }
    }
}
