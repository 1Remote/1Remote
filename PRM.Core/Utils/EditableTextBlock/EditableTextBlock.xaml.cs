using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Shawn.Utils
{
    public partial class EditableTextBlock : UserControl
    {
        public EditableTextBlock()
        {
            InitializeComponent();
            base.Focusable = true;
            base.FocusVisualStyle = null;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TextBox0.Visibility = Visibility.Collapsed;

            var bindingBackground = new Binding("Background") { Mode = BindingMode.OneWay, Source = this };
            Button.SetBinding(BackgroundProperty, bindingBackground);
            var bindingForeground = new Binding("Foreground") { Mode = BindingMode.OneWay, Source = this };
            Button.SetBinding(ForegroundProperty, bindingForeground);

            Button.Focusable = false;

            Button.MouseDoubleClick += (o, args) =>
            {
                BeginEdit();
            };

            TextBox0.KeyDown += (o, args) =>
            {
                switch (args.Key)
                {
                    case Key.Escape:
                        EndEdit(false);
                        break;

                    case Key.Enter:
                        EndEdit(true);
                        break;
                }
            };
        }

        private void TextBoxOnLostFocus(object sender, RoutedEventArgs e)
        {
            EndEdit(true);
        }

        private void BeginEdit()
        {
            if (TextBox0.Visibility != Visibility.Visible)
            {
                TextBox0.Text = _oldText = Text;
                TextBox0.Visibility = Visibility.Visible;
                TextBox0.Focus();
                TextBox0.CaretIndex = TextBox0.Text.Length;
                TextBox0.LostFocus += TextBoxOnLostFocus;
            }
        }

        private void EndEdit(bool isUseNewValue)
        {
            if (TextBox0.Visibility == Visibility.Visible)
            {
                try
                {
                    TextBox0.LostFocus -= TextBoxOnLostFocus;
                }
                finally
                {
                }

                if (isUseNewValue)
                {
                    if (_oldText != TextBox0.Text)
                    {
                        Text = TextBox0.Text;
                        OnTextModified?.Invoke(Text);
                        if (OnTextModifiedCommand?.CanExecute(OnTextModifiedCommandParameter) == true)
                        {
                            OnTextModifiedCommand?.Execute(OnTextModifiedCommandParameter);
                        }
                    }
                }
                else
                    TextBox0.Text = Text = _oldText;
                TextBox0.Visibility = Visibility.Collapsed;
            }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(EditableTextBlock),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnTextChanged)));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditableTextBlock etb)
            {
                etb.TextBox0.Text = (string)e.NewValue;
                etb.Button.Content = (string)e.NewValue;
                etb._oldText = (string)e.NewValue;
            }
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private string _oldText;

        /// <summary>
        /// Invoke when text edit and modified
        /// </summary>
        public Action<string> OnTextModified;

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("OnTextModifiedCommand", typeof(ICommand), typeof(UserControl));

        /// <summary>
        /// Execute when text edit and modified
        /// </summary>
        public ICommand OnTextModifiedCommand
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register("OnTextModifiedCommandParameter", typeof(object), typeof(UserControl));

        public object OnTextModifiedCommandParameter
        {
            get => (object)GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public static readonly DependencyProperty TextTrimmingProperty = DependencyProperty.Register("TextTrimming", typeof(TextTrimming), typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(TextTrimming.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public TextTrimming TextTrimming
        {
            get => (TextTrimming)GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register("IsEditable", typeof(bool), typeof(EditableTextBlock),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnIsEditableChanged)));

        private static void OnIsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditableTextBlock etb)
            {
                etb.Button.IsEnabled = (bool)e.NewValue;
            }
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }
    }
}