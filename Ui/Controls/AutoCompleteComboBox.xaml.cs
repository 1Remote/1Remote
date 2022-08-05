using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace _1RM.Controls
{
    public class ComboboxWithKeyInvoke : ComboBox
    {
        public Action<KeyEventArgs>? OnPreviewKeyDownAction;
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            //base.OnPreviewKeyDown(e);
            OnPreviewKeyDownAction?.Invoke(e);
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            base.OnDropDownOpened(e);

            TextBox tb = (TextBox)this.Template.FindName("PART_EditableTextBox", this);
            tb.Select(tb.Text.Length, 0);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            TextBox tb = (TextBox)this.Template.FindName("PART_EditableTextBox", this);
            tb.Select(tb.Text.Length, 0);
        }
    }
    public partial class AutoCompleteComboBox : UserControl
    {

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(AutoCompleteComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, TextPropertyChangedCallback));

        private static void TextPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteComboBox c
                && e.NewValue is string v)
            {
                TextChanged(c, v);
            }
        }

        public string Text
        {
            get => (string)(GetValue(TextProperty) ?? "");
            set
            {
                if (value != Text)
                {
                    SetValue(TextProperty, value);
                    TextChanged(this, value);
                }
            }
        }

        private static void TextChanged(AutoCompleteComboBox o, string newValue)
        {
            if ((o.Selections?.Count() ?? 0) == 0)
                return;
            if (o._textChangedEnabled == false)
                return;
            o._textChangedEnabled = false;
            Debug.Assert(o?.Selections != null);
            if (string.IsNullOrWhiteSpace(newValue))
            {
                o.CbContent.IsDropDownOpen = false;
                o.Selections4Show = new ObservableCollection<string>(o.Selections);
            }
            else
            {
                o.Selections4Show = new ObservableCollection<string>(o.Selections.Where(x => x.IndexOf(newValue, StringComparison.OrdinalIgnoreCase) >= 0));
                if (o.Selections4Show?.Count() > 0)
                {
                    if (o.CbContent.IsDropDownOpen == false)
                    {
                        o.CbContent.IsDropDownOpen = true;
                        o.CbContent.SelectedIndex = 0;
                    }
                }
                else
                {
                    o.CbContent.IsDropDownOpen = false;
                }
            }
        }

        public static readonly DependencyProperty SelectionsProperty = DependencyProperty.Register(
            nameof(Selections), typeof(IEnumerable<string>), typeof(AutoCompleteComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectionsPropertyChangedCallback));

        private static void SelectionsPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteComboBox cb
                && e.NewValue is IEnumerable<string> selections)
            {
                cb.Selections4Show = new ObservableCollection<string>(selections);
            }
            if (d is AutoCompleteComboBox cb2
                && e.NewValue == null)
            {
                cb2.Selections4Show = new ObservableCollection<string>();
            }
        }

        public IEnumerable<string>? Selections
        {
            get => (IEnumerable<string>)GetValue(SelectionsProperty);
            set
            {
                SetValue(SelectionsProperty, value);
                if (Selections != null)
                    Selections4Show = new ObservableCollection<string>(Selections);
            }
        }

        public static readonly DependencyProperty Selections4ShowProperty = DependencyProperty.Register(
            nameof(Selections4Show), typeof(IEnumerable<string>), typeof(AutoCompleteComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public IEnumerable<string> Selections4Show
        {
            get => (IEnumerable<string>)GetValue(Selections4ShowProperty);
            set => SetValue(Selections4ShowProperty, value);
        }

        public AutoCompleteComboBox()
        {
            InitializeComponent();
            Grid.DataContext = this;
            CbContent.IsTextSearchEnabled = false;
            CbContent.OnPreviewKeyDownAction += HandleUpDown;
        }

        private void CbContent_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                if (CbContent.IsDropDownOpen && Selections4Show.Any())
                {
                    var cmbTextBox = (TextBox)CbContent.Template.FindName("PART_EditableTextBox", CbContent);
                    cmbTextBox.Text = CbContent.SelectedItem?.ToString() ?? Selections4Show.First();
                    cmbTextBox.CaretIndex = cmbTextBox.Text.Length;
                    CbContent.IsDropDownOpen = false;
                }
                e.Handled = true;
            }
        }

        private bool _textChangedEnabled = false;
        private void HandleUpDown(KeyEventArgs e)
        {
            _textChangedEnabled = true;
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (CbContent.IsDropDownOpen == true)
                {
                    int i = 0;
                    int j = CbContent.SelectedIndex;
                    _textChangedEnabled = false;
                    if (Selections4Show.Any(x => x == Text))
                    {
                        i = Selections4Show.ToList().IndexOf(Text);
                    }

                    CbContent.SelectedIndex = i;
                    if (i == j)
                    {
                        if (e.Key == Key.Down && i < Selections4Show.Count() - 1)
                            CbContent.SelectedIndex += 1;
                        else if (e.Key == Key.Up && i > 0)
                            CbContent.SelectedIndex -= 1;
                    }

                    CbContent.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
        }
    }
}