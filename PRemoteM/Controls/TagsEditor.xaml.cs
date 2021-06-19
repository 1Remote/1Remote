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
            ((TagsEditor)d).UpdateSelections();
        }



        public List<string> Tags
        {
            get => (List<string>)GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }


        public static readonly DependencyProperty TagsForSelectProperty = DependencyProperty.Register("TagsForSelect", typeof(List<string>), typeof(TagsEditor), new FrameworkPropertyMetadata(null, OnTagsForSelectPropertyChanged));

        private static void OnTagsForSelectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TagsEditor)d).UpdateSelections();
        }

        public List<string> TagsForSelect
        {
            get => (List<string>)GetValue(TagsForSelectProperty);
            set => SetValue(TagsForSelectProperty, value);
        }

        public TagsEditor()
        {
            InitializeComponent();
        }

        private void UpdateSelections()
        {
            TbNewTag.Selections = TagsForSelect;
        }

        private void AddNewTag()
        {
            var newTag = TbNewTag.Text.Trim();
            if (string.IsNullOrEmpty(newTag) == false && Tags.Contains(TbNewTag.Text.Trim()) == false)
            {
                Tags.Add(TbNewTag.Text.Trim());
                Tags = Tags;
            }
            TbNewTag.Text = "";
            TbNewTag.Selections = TagsForSelect.Where(x => Tags.Contains(x) == false);
        }

        private void TextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewTag();
            }
        }


        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            AddNewTag();
        }

        private void ButtonDel_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                if (Tags.Contains(b.Tag.ToString()))
                {
                    Tags.Remove(b.Tag.ToString());
                    Tags = Tags;
                    TbNewTag.Selections = TagsForSelect.Where(x => Tags.Contains(x) == false);
                }
            }
        }
    }
}
