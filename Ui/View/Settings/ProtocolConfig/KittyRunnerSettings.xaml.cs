using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using _1RM.Model.ProtocolRunner;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class KittyRunnerSettings : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(KittyRunner), typeof(KittyRunnerSettings),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private PropertyChangedEventHandler? _propertyChangedHandler;

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (KittyRunnerSettings)d;
            var oldValue = e.OldValue as KittyRunner;
            var newValue = (KittyRunner)e.NewValue;

            // Unsubscribe from old runner
            if (oldValue != null && control._propertyChangedHandler != null)
            {
                oldValue.PropertyChanged -= control._propertyChangedHandler;
            }

            // Subscribe to new runner
            if (newValue != null)
            {
                control._propertyChangedHandler = (sender, args) =>
                {
                    IoC.Get<ProtocolConfigurationService>().Save();
                };
                newValue.PropertyChanged += control._propertyChangedHandler;
            }

            control.DataContext = newValue;
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
