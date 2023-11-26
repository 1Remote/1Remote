using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using Shawn.Utils;
using Shawn.Utils.Wpf;
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
                    GridBriefNote.Visibility = Visibility.Visible;
                }
                else
                {
                    GridBriefNote.Visibility = Visibility.Collapsed;
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
                    CloseButtonVisibility = Visibility.Collapsed,
                };
            });
        }

        private void NoteTest(FrameworkElement button, MouseEventArgs args)
        {
            if (PopupNoteContent.Content is not NoteDisplayAndEditor editor) return;
            if (!(button.ActualWidth > 0)) return;

            var p1 = args.MouseDevice.GetPosition(button);
#if DEBUG
            SimpleLogHelper.Debug($"ButtonShowNote: {p1.X}, {p1.Y}");
#endif
            bool ret = p1.X > 0
                       && p1.X < button.ActualWidth
                       && p1.Y > 0
                       && p1.Y < button.ActualHeight;
            if (!ret)
            {
                var p3 = args.MouseDevice.GetPosition(editor);
#if DEBUG
                SimpleLogHelper.Debug($"PopupNoteContent: {p3.X}, {p3.Y}, {editor.Main.ActualWidth} X {editor.Main.ActualHeight}");
#endif
                if (p3.X > 0
                    && p3.X < editor.ActualWidth
                    && p3.Y > 0
                    && p3.Y < editor.ActualHeight)
                {
                    ret = true;
                }
            }
            if (ret) return;


            PopupNote.IsOpen = false;
            if (PopupNoteContent.Content is NoteDisplayAndEditor note)
            {
                note.SwitchToView(NoteDisplayAndEditor.View.Normal);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs args)
        {
            if (PopupNote.IsOpen == false) return;
            NoteTest(GridParent, args);
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
            SimpleLogHelper.Warning($"{PopupNote.Placement} {PopupNote.ActualWidth}");
            this.MouseMove -= OnMouseMove;
            this.MouseMove += OnMouseMove;
        }
    }
}
