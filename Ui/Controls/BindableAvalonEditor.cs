using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace _1RM.Controls
{

    public class BindableAvalonEditor : ICSharpCode.AvalonEdit.TextEditor, INotifyPropertyChanged
    {
        /// <summary>
        /// A bindable Text property
        /// </summary>
        public new string Text
        {
            get => (string)GetValue(TextProperty);
            set
            {
                SetValue(TextProperty, value);
                RaisePropertyChanged("Text");
            }
        }

        /// <summary>
        /// The bindable text property dependency property
        /// </summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(BindableAvalonEditor), new FrameworkPropertyMetadata
            {
                DefaultValue = default(string),
                BindsTwoWayByDefault = true,
                PropertyChangedCallback = OnDependencyPropertyChanged
            }
            );

        protected static void OnDependencyPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var target = (BindableAvalonEditor)obj;

            if (target.Document != null)
            {
                var caretOffset = target.CaretOffset;
                var newValue = args.NewValue ?? "";
                target.Document.Text = (string)newValue;
                target.CaretOffset = Math.Min(caretOffset, newValue?.ToString()?.Length ?? 0);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (this.Document != null)
            {
                Text = this.Document.Text;
            }

            base.OnTextChanged(e);
        }

        /// <summary>
        /// Raises a property changed event
        /// </summary>
        /// <param name="property">The name of the property that updates</param>
        public void RaisePropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
