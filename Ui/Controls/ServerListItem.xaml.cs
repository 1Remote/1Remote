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
using PRM.Model;
using PRM.View;
using Shawn.Utils;

namespace PRM.Controls
{
    /// <summary>
    /// Interaction logic for ServerListItem.xaml
    /// </summary>
    public partial class ServerListItem : UserControl
    {
        public static readonly DependencyProperty ProtocolServerViewModelProperty =
            DependencyProperty.Register("ProtocolBaseViewModel", typeof(ProtocolBaseViewModel), typeof(ServerListItem),
                new PropertyMetadata(null, new PropertyChangedCallback(OnDataChanged)));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var value = (ProtocolBaseViewModel)e.NewValue;
            ((ServerListItem)d).DataContext = value;
        }

        public ProtocolBaseViewModel ProtocolBaseViewModel
        {
            get => (ProtocolBaseViewModel)GetValue(ProtocolServerViewModelProperty);
            set => SetValue(ProtocolServerViewModelProperty, value);
        }


        public ServerListItem()
        {
            InitializeComponent();
            this.MouseMove += (sender, args) =>
            {
                if (PopupNote.IsOpen == false) return;
                if (ButtonShowNote.ActualWidth > 0)
                {
                    var p1 = args.MouseDevice.GetPosition(ButtonShowNote);
                    //SimpleLogHelper.Debug($"ButtonShowNote: {p1.X}, {p1.Y}");
                    if (p1.X < 0 || p1.Y < 0)
                        PopupNote.IsOpen = false;
                    else if (p1.Y < ButtonShowNote.ActualHeight && p1.X > ButtonShowNote.ActualWidth)
                        PopupNote.IsOpen = false;
                    else if (p1.Y >= ButtonShowNote.ActualHeight)
                    {
                        var p3 = args.MouseDevice.GetPosition(PopupNoteGrid);
                        if (p3.X > PopupNoteGrid.ActualWidth)
                            PopupNote.IsOpen = false;
                        if (p3.Y > PopupNoteGrid.ActualHeight)
                            PopupNote.IsOpen = false;
                    }
                }
                if (ButtonBriefNote.ActualWidth > 0)
                {
                    var p2 = args.MouseDevice.GetPosition(ButtonBriefNote);
                    SimpleLogHelper.Debug($"ButtonBriefNote: {p2.X}, {p2.Y}, h= {ButtonBriefNote.ActualHeight}, w= {ButtonBriefNote.ActualWidth}");
                    if (p2.X < 0 || p2.Y < 0)
                        PopupNote.IsOpen = false;
                    else if (p2.Y < ButtonBriefNote.ActualHeight && p2.X > ButtonBriefNote.ActualWidth)
                        PopupNote.IsOpen = false;
                    else if (p2.Y >= ButtonBriefNote.ActualHeight)
                    {
                        var p3 = args.MouseDevice.GetPosition(PopupNoteGrid);
                        if (p3.X > PopupNoteGrid.ActualWidth)
                            PopupNote.IsOpen = false;
                        if (p3.Y > PopupNoteGrid.ActualHeight)
                            PopupNote.IsOpen = false;
                    }
                }
            };
        }

        private void BtnSettingMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ProtocolBaseViewModel.Actions = ProtocolBaseViewModel.Server.GetActions();
            PopupCardSettingMenu.IsOpen = true;
        }

        private void ServerMenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            PopupCardSettingMenu.IsOpen = false;
            if (sender is Button { CommandParameter: ProtocolAction afs })
            {
                afs.Run();
            }
        }

        private void ButtonShowNote_OnMouseEnter(object sender, MouseEventArgs e)
        {
            PopupNote.IsOpen = false;
            PopupNote.IsOpen = true;
        }
    }


    public class ConverterNoteToSingleLineNote : IValueConverter
    {
        #region IValueConverter 成员

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string markDown)
            {
                markDown = markDown.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                while (markDown.IndexOf("  ", StringComparison.Ordinal) > 0)
                {
                    markDown = markDown.Replace("  ", " ");
                }

                return markDown.Trim();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion IValueConverter 成员
    }
}
