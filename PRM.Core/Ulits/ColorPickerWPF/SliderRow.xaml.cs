using System.Windows;
using System.Windows.Controls;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for SliderRow.xaml
    /// </summary>
    public partial class SliderRow : UserControl
    {
        public delegate void SliderRowValueChangedHandler(double value);

        public event SliderRowValueChangedHandler OnValueChanged;

        public string FormatString { get; set; }

        protected bool UpdatingValues = false;

        public SliderRow()
        {
            FormatString = "F2";

            InitializeComponent();
        }

        private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Set textbox
            var value = Slider.Value;

            if (!UpdatingValues)
            {
                UpdatingValues = true;
                TextBox.Text = value.ToString(FormatString);
                OnValueChanged?.Invoke(value);
                UpdatingValues = false;
            }
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!UpdatingValues)
            {
                var text = TextBox.Text;
                bool ok = false;
                double parsedValue = 0;

                ok = double.TryParse(text, out parsedValue);
                if (ok)
                {
                    UpdatingValues = true;
                    Slider.Value = parsedValue;
                    OnValueChanged?.Invoke(parsedValue);
                    UpdatingValues = false;
                }
            }
        }
    }
}
