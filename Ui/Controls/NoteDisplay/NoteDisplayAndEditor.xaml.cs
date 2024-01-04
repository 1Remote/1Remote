using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using Shawn.Utils;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Stylet;

namespace _1RM.Controls.NoteDisplay
{
    public partial class NoteDisplayAndEditor : UserControl
    {
        public static readonly DependencyProperty ServerProperty =
            DependencyProperty.Register("Server", typeof(ProtocolBase), typeof(NoteDisplayAndEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnServerChanged));
        private static void OnServerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not NoteDisplayAndEditor control) return;

            control.SwitchToView(View.Normal);
            if (e.OldValue is ProtocolBase server0)
            {
                server0.PropertyChanged -= control.ServerOnPropertyChanged;
            }
            if (e.NewValue is ProtocolBase server1)
            {
                server1.PropertyChanged += control.ServerOnPropertyChanged;
                control.EditEnable = control.EditEnable && server1.DataSource?.IsWritable == true;
            }
        }

        private void ServerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBase.Note))
            {
                MarkdownViewer.Markdown = Server?.Note ?? "";
            }
        }

        public ProtocolBase? Server
        {
            get => (ProtocolBase)GetValue(ServerProperty);
            set => SetValue(ServerProperty, value);
        }

        // a callback for save launcher settings
        public static readonly DependencyProperty CommandOnCloseRequestProperty = DependencyProperty.Register(
            "CommandOnCloseRequest", typeof(RelayCommand), typeof(NoteDisplayAndEditor), new FrameworkPropertyMetadata(default(RelayCommand), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public RelayCommand CommandOnCloseRequest
        {
            get => (RelayCommand)GetValue(CommandOnCloseRequestProperty);
            set => SetValue(CommandOnCloseRequestProperty, value);
        }

        public Visibility CloseButtonVisibility { get; set; } = Visibility.Collapsed;
        public Visibility EditButtonVisibility { get; set; } = Visibility.Visible;
        public bool EditEnable { get; set; }

        public NoteDisplayAndEditor()
        {
            InitializeComponent();
            Loaded += NoteDisplayAndEditor_Loaded;
        }

        private void NoteDisplayAndEditor_Loaded(object sender, RoutedEventArgs e)
        {
            SwitchToView(View.Normal);
        }

        private void OpenHyperlink(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            try
            {
                var url = e?.Parameter?.ToString();
                if (url != null)
                {
                    HyperlinkHelper.OpenUriBySystem(url);
                }
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
            }
        }

        private void ClickOnImage(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            try
            {
                var url = e?.Parameter?.ToString();
                if (url != null)
                {
                    HyperlinkHelper.OpenUriBySystem(url);
                }
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
            }
        }

        public enum View
        {
            Normal,
            Editor,
            Copy
        }
        private View? _currentView = null;
        public void SwitchToView(NoteDisplayAndEditor.View v)
        {
            ButtonClose.Visibility = CloseButtonVisibility;
            ButtonEdit.Visibility = EditButtonVisibility;
            Execute.OnUIThreadSync(() =>
            {
                switch (v)
                {
                    case View.Normal:
                        MarkdownViewer.Markdown = Server?.Note ?? "";
                        MarkdownViewer.Visibility = Visibility.Visible;
                        GridEditor.Visibility = Visibility.Collapsed;
                        break;
                    case View.Editor:
                        TbMarkdown.Text = Server?.Note ?? "";
                        GridButtons.Visibility = Visibility.Visible;
                        ButtonSave.IsEnabled = true;
                        TbMarkdown.IsReadOnly = false;
                        MarkdownViewer.Visibility = Visibility.Collapsed;
                        GridEditor.Visibility = Visibility.Visible;
                        break;
                    case View.Copy:
                        if (_currentView == View.Normal)
                        {
                            TbMarkdown.Text = Server?.Note ?? "";
                            // 只能拷贝，不能编辑
                            GridButtons.Visibility = Visibility.Hidden;
                            ButtonSave.IsEnabled = false;
                            TbMarkdown.IsReadOnly = true;
                            MarkdownViewer.Visibility = Visibility.Collapsed;
                            GridEditor.Visibility = Visibility.Visible;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(v), v, null);
                }
                _currentView = v;
            });
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (Server?.DataSource?.IsWritable == true
                && Server.Note.Trim() != TbMarkdown.Text.Trim())
            {
                Server.Note = TbMarkdown.Text.Trim();
                IoC.Get<GlobalData>().UpdateServer(Server);
            }
            SwitchToView(View.Normal);
        }

        private void ButtonCancelEdit_OnClick(object sender, RoutedEventArgs e)
        {
            SwitchToView(View.Normal);
        }

        private void ButtonNoteStartEdit_OnClick(object sender, RoutedEventArgs e)
        {
            TbMarkdown.Text = Server?.Note ?? "";
            if (Server?.DataSource?.IsWritable == true)
            {
                SwitchToView(View.Editor);
            }
            else
            {
                SwitchToView(View.Copy);
            }
        }

        private void GridEditor_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (_currentView == View.Normal)
                SwitchToView(View.Copy);
        }

        private void GridEditor_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_currentView == View.Copy)
                SwitchToView(View.Normal);
        }

        private void TbMarkdown_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentView == View.Copy
                && Server?.DataSource?.IsWritable == true)
            {
                SwitchToView(View.Editor);
            }
        }

        private void ButtonClose_OnClick(object sender, RoutedEventArgs e)
        {
            CommandOnCloseRequest?.Execute();
        }

        private void TbMarkdown_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }

        private void Ignore_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Ignore_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            e.Handled = true;
        }
    }
}
