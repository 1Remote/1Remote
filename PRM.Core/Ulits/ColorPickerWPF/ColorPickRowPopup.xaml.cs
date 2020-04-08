using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ColorPickerWPF.Code;
using UserControl = System.Windows.Controls.UserControl;

namespace ColorPickerWPF
{
    /// <summary>
    /// Interaction logic for ColorPickRowPopup.xaml
    /// </summary>
    public partial class ColorPickRowPopup : UserControl, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public Action<Color> OnColorSelected;

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color), typeof(ColorPickRowPopup),
          new PropertyMetadata(Colors.White, OnDataChanged));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (Color)e.NewValue;
            ((ColorPickRowPopup)d).ColorDisplayGrid.Background = new SolidColorBrush(value);
            ((ColorPickRowPopup)d).HexLabel.Text = value.ToHexString();
            ((ColorPickRowPopup)d).OnPropertyChanged(nameof(Color));
        }

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set
            {
                SetValue(ColorProperty, value);
                OnPropertyChanged(nameof(Color));
                OnColorSelected?.Invoke(value);
            }
        }

        public string ColorHex => Color.ToHexString();

        public ColorPickRowPopup()
        {
            InitializeComponent();
            this.DataContext = this;
            ColorPicker.OnPickColor += color => { SetColor(color); };
        }

        private void PickColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopupPicker.Focus();
            PopupPicker.IsOpen = true;
        }

        public void SetColor(Color color)
        {
            Color = color;
            HexLabel.Text = color.ToHexString();
            ColorDisplayGrid.Background = new SolidColorBrush(color);
        }
    }
}
