using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Base;
using PRM.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// SearchBoxWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchBoxWindow : Window
    {
        private Point lastPoint = new Point(0, 0);
        private readonly object _keyDownLocker = new object();
        private readonly VmSearchBox _vmSearchBox = null;


        public SearchBoxWindow(VmMain vmMain)
        {
            InitializeComponent();
            _vmSearchBox = new VmSearchBox(vmMain);
            DataContext = _vmSearchBox;
            TbKeyWord.Focus();

#if !DEBUG
            // close when Deactivated
            Deactivated += (sender, args) => { Close(); };
            KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape) Close();
            };
#endif
        }



        private void WindowHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }




        private void TbKeyWord_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            if (e.Key == Key.Enter)
            {
                // TODO open conn
                if (_vmSearchBox.DispServerList.Count > _vmSearchBox.SelectedServerTextIndex)
                    MessageBox.Show(_vmSearchBox.DispServerList[_vmSearchBox.SelectedServerTextIndex].DispName);
                Close();
            }

            if (e.Key == Key.Down)
            {
                lock (_keyDownLocker)
                {
                    if (_vmSearchBox.SelectedServerTextIndex < _vmSearchBox.DispServerList.Count - 1)
                    {
                        ++_vmSearchBox.SelectedServerTextIndex;
                        ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                    }
                }
            }
            if (e.Key == Key.Up)
            {
                lock (_keyDownLocker)
                {
                    if (_vmSearchBox.SelectedServerTextIndex > 0)
                    {
                        --_vmSearchBox.SelectedServerTextIndex;
                        ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                    }
                }
            }
            if (e.Key == Key.PageUp)
            {
                lock (_keyDownLocker)
                {
                    var i = _vmSearchBox.SelectedServerTextIndex - 5;
                    if (i < 0)
                        i = 0;
                    _vmSearchBox.SelectedServerTextIndex = i;
                    ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                }
            }
            if (e.Key == Key.PageDown)
            {
                lock (_keyDownLocker)
                {
                    var i = _vmSearchBox.SelectedServerTextIndex + 5;
                    if (i > _vmSearchBox.DispServerList.Count - 1)
                        i = _vmSearchBox.DispServerList.Count - 1;
                    _vmSearchBox.SelectedServerTextIndex = i;
                    ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
                }
            }
        }
    }
}
