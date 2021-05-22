using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PRM.Controls
{
    public partial class TagsEditor : UserControl
    {
        public static readonly DependencyProperty TagsProperty = DependencyProperty.Register("Tags", typeof(List<string>), typeof(TagsEditor),
            new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (List<string>)e.NewValue;
            ((TagsEditor)d).DataContext = value;
        }

        public List<string> Tags
        {
            get => (List<string>)GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }

        public TagsEditor()
        {
            InitializeComponent();
        }
    }
}
