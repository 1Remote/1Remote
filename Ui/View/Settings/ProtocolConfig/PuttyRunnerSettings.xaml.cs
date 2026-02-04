using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;

namespace _1RM.View.Settings.ProtocolConfig
{
    public partial class PuttyRunnerSettings : UserControl
    {
        public static readonly DependencyProperty RunnerProperty =
            DependencyProperty.Register("Runner", typeof(PuttyRunner), typeof(PuttyRunnerSettings),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private PropertyChangedEventHandler? _propertyChangedHandler;

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PuttyRunnerSettings)d;
            var oldValue = e.OldValue as PuttyRunner;
            var newValue = (PuttyRunner)e.NewValue;

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

        public PuttyRunner Runner
        {
            get => (PuttyRunner)GetValue(RunnerProperty);
            set => SetValue(RunnerProperty, value);
        }


        public PuttyRunnerSettings()
        {
            InitializeComponent();

            var fonts = new Dictionary<string, string>();
            // Enumerate the current set of system fonts,
            // and fill the combo box with the names of the fonts.
            foreach (var fontFamily in Fonts.SystemFontFamilies)
            {
                // FontFamily.Source contains the font family name.
                CbFonts.Items.Add(fontFamily.FamilyNames.Last().Value);
            }
        }
    }
}
