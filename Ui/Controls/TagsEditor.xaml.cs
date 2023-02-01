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
using _1RM.Utils;

namespace _1RM.Controls
{
    public partial class TagsEditor : UserControl
    {
        public static readonly DependencyProperty TagsProperty =
            DependencyProperty.Register("Tags", typeof(List<string>), typeof(TagsEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTagsPropertyChanged));

        private static void OnTagsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TagsEditor)d).UpdateSelections();
        }
        public List<string> Tags
        {
            get
            {
                var obj = GetValue(TagsProperty);
                return obj == null ? new List<string>() : (List<string>)GetValue(TagsProperty);
            }
            set => SetValue(TagsProperty, value);
        }


        public static readonly DependencyProperty TagsForSelectProperty = DependencyProperty.Register("TagsForSelect", typeof(List<string>), typeof(TagsEditor), new FrameworkPropertyMetadata(null, OnTagsForSelectPropertyChanged));
        private static void OnTagsForSelectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TagsEditor)d).UpdateSelections();
        }
        public List<string> TagsForSelect
        {
            get
            {
                var obj = GetValue(TagsProperty);
                return obj == null ? new List<string>() : (List<string>)GetValue(TagsForSelectProperty);
            }
            set => SetValue(TagsForSelectProperty, value);
        }


        public TagsEditor()
        {
            InitializeComponent();
            TbNewTag.OnSelectionConfirm += AddNewTag;
        }

        private void UpdateSelections()
        {
            if (TagsForSelect != null && Tags != null) // must null check
            {
                TbNewTag.Selections = Tags?.Count > 0 ? TagsForSelect.Where(x => Tags?.Contains(x) != true)
                    : TagsForSelect;
            }
        }

        private void AddNewTag(string newTag)
        {
            newTag = newTag.Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(newTag) == false && Tags.Contains(newTag) != true)
            {
                Tags.Add(TagAndKeywordEncodeHelper.RectifyTagName(newTag));
            }
            TbNewTag.Text = "";
            Tags = Tags; // raise notify
            TbNewTag.Selections = TagsForSelect.Where(x => Tags?.Contains(x) != true);
        }

        private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                AddNewTag(TbNewTag.Text);
                e.Handled = true;
            }
            else if (TbNewTag.Text.IndexOf(" ", StringComparison.Ordinal) > 0)
            {
                TbNewTag.Text = TbNewTag.Text.Replace(" ", "");
            }
        }


        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            AddNewTag(TbNewTag.Text.Trim());
        }

        private void ButtonDel_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                if (Tags.Contains(b.Tag.ToString()!))
                {
                    Tags.Remove(b.Tag.ToString()!);
                    Tags = Tags; // raise notify
                    TbNewTag.Selections = TagsForSelect.Where(x => Tags.Contains(x) == false);
                }
            }
        }
    }
}
