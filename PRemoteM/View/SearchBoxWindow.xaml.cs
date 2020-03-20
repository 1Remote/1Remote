using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PRM.Core.Base;
using PRM.Core.ViewModel;

namespace PRM.View
{
    /// <summary>
    /// SearchBoxWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchBoxWindow : Window
    {
        private Point lastPoint = new Point(0, 0);
        private readonly object _keyDownLocker = new object();
        //private readonly VmSearchBox _vmSearchBox = null;


        public SearchBoxWindow(VmMain vmMain)
        {
            InitializeComponent();
            //_vmSearchBox = new VmSearchBox(vmMain);
            //DataContext = _vmSearchBox;
            TbKeyWord.Focus();



            //lastPoint = new Point(this.Left, this.Top);
            //PreviewMouseDown += (sender, args) =>
            //{
            //    lastPoint = this.PointToScreen(new Point(0, 0));
            //};
            //PreviewMouseUp += (sender, args) =>
            //{
            //    if (PopupSelections.IsOpen)
            //    {
            //        var thisPoint = new Point(this.Left, this.Top);
            //        if (Math.Abs(thisPoint.X - lastPoint.X) > 0.5 ||
            //            Math.Abs(thisPoint.Y - lastPoint.Y) > 0.5)
            //        {
            //            PopupSelections.IsOpen = false;
            //        }
            //    }
            //};


            //Deactivated += (sender, args) => { Close(); };
            //KeyDown += (sender, args) =>
            //{
            //    if (args.Key == Key.Escape) Close();
            //};
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
            //if (e.Key == Key.Escape)
            //{
            //    Close();
            //}
            //if (e.Key == Key.Enter)
            //{
            //    if(_vmSearchBox.DispServerList.Count > _vmSearchBox.SelectedServerTextIndex)
            //        MessageBox.Show(_vmSearchBox.DispServerList[_vmSearchBox.SelectedServerTextIndex].DispName);
            //    // TODO open
            //    Close();
            //}

            //if (e.Key == Key.Down)
            //{
            //    lock (_keyDownLocker)
            //    {
            //        if (_vmSearchBox.SelectedServerTextIndex < _vmSearchBox.DispServerList.Count - 1)
            //        {
            //            ++_vmSearchBox.SelectedServerTextIndex;
            //            ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
            //        }
            //    }
            //}
            //if (e.Key == Key.Up)
            //{
            //    lock (_keyDownLocker)
            //    {
            //        if (_vmSearchBox.SelectedServerTextIndex > 0)
            //        {
            //            --_vmSearchBox.SelectedServerTextIndex;
            //            ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
            //        }
            //    }
            //}
            //if (e.Key == Key.PageUp)
            //{
            //    lock (_keyDownLocker)
            //    {
            //        var i = _vmSearchBox.SelectedServerTextIndex - 5;
            //        if (i < 0)
            //            i = 0;
            //        _vmSearchBox.SelectedServerTextIndex = i;
            //        ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
            //    }
            //}
            //if (e.Key == Key.PageDown)
            //{
            //    lock (_keyDownLocker)
            //    {
            //        var i = _vmSearchBox.SelectedServerTextIndex + 5;
            //        if (i > _vmSearchBox.DispServerList.Count - 1)
            //            i = _vmSearchBox.DispServerList.Count - 1;
            //        _vmSearchBox.SelectedServerTextIndex = i;
            //        ListBoxSelections.ScrollIntoView(ListBoxSelections.SelectedItem);
            //    }
            //}
        }
    }
}
