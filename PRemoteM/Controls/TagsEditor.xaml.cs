using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PRM.Annotations;
using PRM.Core;

namespace PRM.Controls
{
    public partial class TagsEditor : UserControl
    {
        public static readonly DependencyProperty TagsProperty =
            DependencyProperty.Register("Tags", typeof(List<string>), typeof(TagsEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTagsPropertyChanged));

        private static void OnTagsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (List<string>)e.NewValue;
            if (value?.Count > 0)
                ((TagsEditor)d).SetTextBox(string.Join(", ", value));
            else
                ((TagsEditor)d).SetTextBox("");
            ((TagsEditor)d).UpdateCheckBoxFromTags();
        }



        public List<string> Tags
        {
            get => (List<string>)GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }


        public static readonly DependencyProperty TagsForSelectProperty = DependencyProperty.Register("TagsForSelect", typeof(List<string>), typeof(TagsEditor), new FrameworkPropertyMetadata(null, OnTagsForSelectPropertyChanged));

        private static void OnTagsForSelectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //var value = (List<string>)e.NewValue;
            ((TagsEditor)d).UpdateCheckBoxFromTags();
        }

        public List<string> TagsForSelect
        {
            get => (List<string>)GetValue(TagsForSelectProperty);
            set => SetValue(TagsForSelectProperty, value);
        }

        private readonly Timer _t = new Timer();
        public TagsEditor()
        {
            InitializeComponent();
            ListViewTags.MouseMove += ListViewTagsOnMouseMove;
            TextBox.MouseMove += ListViewTagsOnMouseMove;
            TextBox.KeyDown += TextBoxOnKeyDown;
            TextBox.LostFocus += (sender, args) => Parse();
            this.LostFocus += (sender, args) => Parse();
            ListViewTagsForSelect.SelectionChanged += Selector_OnSelectionChanged;
            ListViewTagsForSelect.MouseLeave += (sender, args) => ListViewTagsForSelectClose();

            if (Tags?.Count > 0)
                TextBox.Text = string.Join(", ", Tags);
            _t.AutoReset = true;
            _t.Interval = 200;
            _t.Elapsed += TOnElapsed;
            _t.Start();

            this.Unloaded += (sender, args) =>
            {
                _t.Dispose();
            };
        }

        ~TagsEditor()
        {
            _t.Dispose();
        }

        private void UpdateCheckBoxFromTags()
        {
            ListViewTagsForSelect.SelectionChanged -= Selector_OnSelectionChanged;
            ListViewTagsForSelect.SelectedItems.Clear();
            if (TagsForSelect != null && Tags != null)
            {
                foreach (var str in TagsForSelect)
                {
                    if (Tags.Contains(str.ToString()) && ListViewTagsForSelect.SelectedItems.Contains(str) == false)
                        ListViewTagsForSelect.SelectedItems.Add(str);
                }
            }
            ListViewTagsForSelect.SelectionChanged += Selector_OnSelectionChanged;

            if (Tags?.Count == 0)
                TextBox.Visibility = Visibility.Visible;
        }

        private void ListViewTagsOnMouseMove(object sender, MouseEventArgs e)
        {
            if (ListViewTags.IsMouseOver == true || TextBox.IsMouseOver == true)
            {
                _lastMouseMove = DateTime.Now;
            }

            if (TextBox.Visibility != Visibility.Visible)
            {
                TextBox.Visibility = Visibility.Visible;
                ListViewTags.Visibility = Visibility.Hidden;
            }
        }

        private string _lastValue = "";
        private DateTime _lastMouseMove = DateTime.MinValue;
        private DateTime _lastKeyDownTime = DateTime.MinValue;
        private void TOnElapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                //if (TextBox.IsFocused == false
                //    && (DateTime.Now - _lastKeyDownTime).TotalMilliseconds > 1000)
                //{
                //    Parse();
                //}
                
                if ((DateTime.Now - _lastKeyDownTime).TotalMilliseconds > 1000
                    && (DateTime.Now - _lastMouseMove).TotalMilliseconds > 1000)
                    if (ListViewTags.IsMouseOver == false && TextBox.IsMouseOver == false && string.IsNullOrWhiteSpace(TextBox.Text) == false)
                    {
                        Parse();
                    }
            });
        }

        public void Parse()
        {
            TextBox.Visibility = Visibility.Hidden;
            ListViewTags.Visibility = Visibility.Visible;
            if (_lastValue != TextBox.Text)
            {
                var newTags = TextBox.Text.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct().OrderBy(x => x).ToList();
                if (newTags.Count != Tags.Count || newTags.Any(x => !Tags.Contains(x)))
                {
                    Tags = newTags;
                }

                SetTextBox(string.Join(", ", Tags));
                _lastValue = TextBox.Text;
                TextBox.CaretIndex = TextBox.Text.Length;
            }
        }

        public void SetTextBox(string tags)
        {
            if (TextBox.Text != tags)
            {
                TextBox.Text = tags;
                TextBox.CaretIndex = TextBox.Text.Length;
            }
        }


        private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            _lastKeyDownTime = DateTime.Now;

            if (e.Key == Key.Enter)
            {
                Parse();
                TextBox.CaretIndex = TextBox.Text.Length;
            }
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (from object str in ListViewTagsForSelect.SelectedItems select str.ToString());

            var tags = selected.ToList();
            tags.AddRange(Tags.Where(str => TagsForSelect.Contains(str) == false));
            Tags = tags.Distinct().OrderBy(x => x).ToList();
        }

        private void ListViewTagsForSelectHeightAnimation(double to)
        {
            var animation = new DoubleAnimation
            {
                From = ListViewTagsForSelect.Height,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                AccelerationRatio = 0.9,
            };
            ListViewTagsForSelect.BeginAnimation(HeightProperty, null);
            ListViewTagsForSelect.BeginAnimation(HeightProperty, animation);
        }

        private void ListViewTagsForSelectOpen()
        {
            if (Math.Abs(ListViewTagsForSelect.Height) < 1)
            {
                ListViewTagsForSelectHeightAnimation(197);
            }
        }

        private void ListViewTagsForSelectClose()
        {
            if (ListViewTagsForSelect.Height > 0.5)
            {
                ListViewTagsForSelectHeightAnimation(0);
            }
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (ListViewTagsForSelect.Height > 0)
                ListViewTagsForSelectClose();
            else
                ListViewTagsForSelectOpen();
        }
    }
}
