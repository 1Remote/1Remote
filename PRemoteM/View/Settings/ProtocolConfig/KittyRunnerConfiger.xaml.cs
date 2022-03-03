using System.Windows;
using System.Windows.Controls;
using PRM.Model.Protocol.Runner.Default;

namespace PRM.View.Settings.ProtocolConfig
{
    public partial class KittyRunnerConfiger : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(KittyRunner), typeof(KittyRunnerConfiger),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (KittyRunner)e.NewValue;
            ((KittyRunnerConfiger)d).DataContext = value;
        }

        public KittyRunner Runner
        {
            get => (KittyRunner)GetValue(RunnerProperty);
            set => SetValue(RunnerProperty, value);
        }


        public KittyRunnerConfiger()
        {
            InitializeComponent();
        }
    }
}
