using System.Windows;
using System.Windows.Controls;
using PRM.Core.Protocol.Runner.Default;

namespace PRM.View.ProtocolConfig
{
    public partial class PuttyRunnerConfiger : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(SshDefaultRunner), typeof(PuttyRunnerConfiger),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (SshDefaultRunner)e.NewValue;
            ((PuttyRunnerConfiger)d).DataContext = value;
        }

        public SshDefaultRunner Runner
        {
            get => (SshDefaultRunner)GetValue(RunnerProperty);
            set => SetValue(RunnerProperty, value);
        }


        public PuttyRunnerConfiger()
        {
            InitializeComponent();
        }
    }
}
