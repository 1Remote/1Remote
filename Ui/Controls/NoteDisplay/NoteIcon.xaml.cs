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

        private NoteDisplayAndEditor? _noteDisplayAndEditor;
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
                    EditEnable = Server.DataSource?.IsWritable == true,
                    CloseEnable = false,
                };
            });
        }

        private bool NoteTest(Button button, MouseEventArgs args)
        {
            if (PopupNoteContent.Content is not NoteDisplayAndEditor editor)
            {
                return false;
            }
            if (button.ActualWidth > 0)
            {
                var p1 = args.MouseDevice.GetPosition(button);
                SimpleLogHelper.Debug($"ButtonShowNote: {p1.X}, {p1.Y}");
                if (p1.Y < button.ActualHeight)
                {
                    if (p1.X < 0 || p1.Y < 0 || p1.X > button.ActualWidth)
                        PopupNote.IsOpen = false;
                }
                else
                {
                    var p3 = args.MouseDevice.GetPosition(editor);
                    SimpleLogHelper.Debug($"PopupNoteContent: {p3.X}, {p3.Y}, {editor.Main.ActualWidth} X {editor.Main.ActualHeight}");
                    if (p3.X < 0)
                        PopupNote.IsOpen = false;
                    if (p3.X > editor.Main.ActualWidth)
                        PopupNote.IsOpen = false;
                    if (p3.Y > editor.Main.ActualHeight)
                        PopupNote.IsOpen = false;
                }
                return true;
            }

            return false;
        }

        private void OnMouseMove(object sender, MouseEventArgs args)
        {
            if (PopupNote.IsOpen == false) return;
            NoteTest(ButtonShowNote, args);
            NoteTest(ButtonBriefNote, args);
        }


        private async void ButtonShowNote_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (PopupNoteContent.Content is not NoteDisplayAndEditor
                && _noteDisplayAndEditor != null)
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
            if (PopupNoteContent.Content is not NoteDisplayAndEditor
                && _noteDisplayAndEditor != null)
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
