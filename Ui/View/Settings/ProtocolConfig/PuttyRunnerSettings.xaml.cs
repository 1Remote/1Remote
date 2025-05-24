using System.Windows;
using System.Windows.Controls;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class PuttyRunnerSettings : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(PuttyRunner), typeof(PuttyRunnerSettings),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (PuttyRunner)e.NewValue;
            ((PuttyRunnerSettings)d).DataContext = value;
        }

        public PuttyRunner Runner
        {
            get => (PuttyRunner)GetValue(RunnerProperty);
            set
            {
                SetValue(RunnerProperty, value);
                value.PropertyChanged += (sender, args) =>
                {
                    IoC.Get<ProtocolConfigurationService>().Save();
                };
            }
        }


        public PuttyRunnerSettings()
        {
            InitializeComponent();
        }
    }
}
