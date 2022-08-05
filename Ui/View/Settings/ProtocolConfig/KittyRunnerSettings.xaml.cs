using System.Windows;
using System.Windows.Controls;
using _1RM.Model.ProtocolRunner.Default;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class KittyRunnerSettings : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(KittyRunner), typeof(KittyRunnerSettings),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (KittyRunner)e.NewValue;
            ((KittyRunnerSettings)d).DataContext = value;
        }

        public KittyRunner Runner
        {
            get => (KittyRunner)GetValue(RunnerProperty);
            set => SetValue(RunnerProperty, value);
        }


        public KittyRunnerSettings()
        {
            InitializeComponent();
        }
    }
}
