using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
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
            var server1 = e.NewValue as ProtocolBase;
            var server0 = e.OldValue as ProtocolBase;
            if (d is NoteDisplayAndEditor control)
            {
                control.EndEdit();
                if (server0 != null)
                    server0.PropertyChanged -= control.ServerOnPropertyChanged;
                if (server1 != null)
                    server1.PropertyChanged += control.ServerOnPropertyChanged;
                control.EditEnable = control.EditEnable && server1?.GetDataSource()?.IsWritable == true;
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


        public static readonly DependencyProperty CommandOnCloseRequestProperty = DependencyProperty.Register(
            "CommandOnCloseRequest", typeof(RelayCommand), typeof(NoteDisplayAndEditor), new FrameworkPropertyMetadata(default(RelayCommand), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public RelayCommand CommandOnCloseRequest
        {
            get => (RelayCommand)GetValue(CommandOnCloseRequestProperty);
            set => SetValue(CommandOnCloseRequestProperty, value);
        }
        
        public bool CloseEnable { get; set; }

        private bool _editEnable;
        public bool EditEnable
        {
            get => _editEnable;
            set
            {
                _editEnable = value;
                if (IsLoaded)
                {
                    Execute.OnUIThreadSync(() =>
                    {
                        ButtonEdit.IsEnabled = EditEnable;
                        ButtonEdit.Visibility = EditEnable ? Visibility.Visible : Visibility.Collapsed;
                    });
                }
            }
        }

        public NoteDisplayAndEditor()
        {
            InitializeComponent();
            Loaded += NoteDisplayAndEditor_Loaded;
        }

        private void NoteDisplayAndEditor_Loaded(object sender, RoutedEventArgs e)
        {
            ButtonEdit.IsEnabled = EditEnable;
            ButtonEdit.Visibility = EditEnable ? Visibility.Visible : Visibility.Collapsed;
            ButtonClose.IsEnabled = CloseEnable;
            ButtonClose.Visibility = CloseEnable ? Visibility.Visible : Visibility.Collapsed;
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
            //MessageBox.Show($"URL: {e.Parameter}");
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

        private void EndEdit()
        {
            MarkdownViewer.Markdown = Server?.Note ?? "";
            MarkdownViewer.Visibility = Visibility.Visible;
            GridEditor.Visibility = Visibility.Collapsed;
        }
        private void StartEdit()
        {
            MarkdownViewer.Visibility = Visibility.Collapsed;
            GridEditor.Visibility = Visibility.Visible;
        }

        private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (Server != null && Server.Note.Trim() != TbMarkdown.Text.Trim())
            {
                Server.Note = TbMarkdown.Text.Trim();
                IoC.Get<GlobalData>().UpdateServer(Server);
            }
            EndEdit();
        }

        private void ButtonCancelEdit_OnClick(object sender, RoutedEventArgs e)
        {
            EndEdit();
        }

        private void ButtonNoteStartEdit_OnClick(object sender, RoutedEventArgs e)
        {
            TbMarkdown.Text = Server?.Note ?? "";
            StartEdit();
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
