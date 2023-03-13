using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using Shawn.Utils;
using Stylet;

namespace _1RM.Controls.NoteDisplay
{
    /// <summary>
    /// NoteIcon.xaml 的交互逻辑
    /// </summary>
    public partial class NoteIcon : UserControl, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isBriefNoteShown = false;
        public bool IsBriefNoteShown
        {
            get => _isBriefNoteShown;
            set
            {
                _isBriefNoteShown = value;
                if (value)
                {
                    ButtonBriefNote.Visibility = Visibility.Visible;
                    ButtonShowNote.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ButtonBriefNote.Visibility = Visibility.Collapsed;
                    ButtonShowNote.Visibility = Visibility.Visible;
                }
            }
        }

        public ProtocolBase Server { get; }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private NoteDisplayAndEditor _noteDisplayAndEditor;
        public NoteIcon(ProtocolBase server)
        {
            Server = server;
            InitializeComponent();
            IsBriefNoteShown = false;
            Execute.OnUIThreadSync(() =>
            {
                _noteDisplayAndEditor = new NoteDisplayAndEditor()
                {
                    Server = Server,
                    Width = 400,
                    Height = 300,
                    EditEnable = Server.GetDataSource()?.IsWritable == true,
                    CloseEnable = false,
                };
            });
        }

        private void OnMouseMove(object sender, MouseEventArgs args)
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
                    var p3 = args.MouseDevice.GetPosition(PopupNoteContent);
                    if (p3.X > PopupNoteContent.ActualWidth)
                        PopupNote.IsOpen = false;
                    if (p3.Y > PopupNoteContent.ActualHeight)
                        PopupNote.IsOpen = false;
                }
            }
            if (ButtonBriefNote.ActualWidth > 0)
            {
                var p2 = args.MouseDevice.GetPosition(ButtonBriefNote);
                //SimpleLogHelper.Debug($"ButtonBriefNote: {p2.X}, {p2.Y}, h= {ButtonBriefNote.ActualHeight}, w= {ButtonBriefNote.ActualWidth}");
                if (p2.X < 0 || p2.Y < 0)
                    PopupNote.IsOpen = false;
                else if (p2.Y < ButtonBriefNote.ActualHeight && p2.X > ButtonBriefNote.ActualWidth)
                    PopupNote.IsOpen = false;
                else if (p2.Y >= ButtonBriefNote.ActualHeight)
                {
                    var p3 = args.MouseDevice.GetPosition(PopupNoteContent);
                    if (p3.X > PopupNoteContent.ActualWidth)
                        PopupNote.IsOpen = false;
                    if (p3.Y > PopupNoteContent.ActualHeight)
                        PopupNote.IsOpen = false;
                }
            }

            if (PopupNote.IsOpen == false)
            {
                this.MouseMove -= OnMouseMove;
                PopupNoteContent.Content = null;
            }
        }


        private async void ButtonShowNote_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (PopupNoteContent.Content is not NoteDisplayAndEditor)
            {
                PopupNoteContent.Content = _noteDisplayAndEditor;
            }
            await Task.Yield();
            PopupNote.IsOpen = false;
            await Task.Yield();
            PopupNote.IsOpen = true;
            this.MouseMove -= OnMouseMove;
            this.MouseMove += OnMouseMove;
        }

        private async void ButtonBriefNote_OnClick(object sender, RoutedEventArgs e)
        {
            if (PopupNoteContent.Content is not NoteDisplayAndEditor)
            {
                PopupNoteContent.Content = _noteDisplayAndEditor;
            }
            await Task.Yield();
            PopupNote.IsOpen = false;
            await Task.Yield();
            PopupNote.IsOpen = true;
            this.MouseMove -= OnMouseMove;
            this.MouseMove += OnMouseMove;
        }
    }
}
