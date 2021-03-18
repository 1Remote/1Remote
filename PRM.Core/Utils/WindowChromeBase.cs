using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Shawn.Utils
{
    public abstract class WindowChromeBase : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            if (NotifyPropertyChangedEnabled)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void SetAndNotifyIfChanged<T>(string propertyName, ref T oldValue, T newValue)
        {
            if (oldValue == null && newValue == null) return;
            if (oldValue != null && oldValue.Equals(newValue)) return;
            if (newValue != null && newValue.Equals(oldValue)) return;
            oldValue = newValue;
            RaisePropertyChanged(propertyName);
        }

        #endregion INotifyPropertyChanged

        #region DragMove

        protected bool _isLeftMouseDown = false;
        protected bool _isDragging = false;

        protected virtual void WinTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _isLeftMouseDown = false;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
            }
            else
            {
                _isLeftMouseDown = true;
                var th = new Thread(() =>
                {
                    Thread.Sleep(50);
                    if (_isLeftMouseDown)
                    {
                        _isDragging = true;
                    }
                    _isLeftMouseDown = false;
                });
                th.Start();
            }
        }

        protected virtual void WinTitleBar_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isLeftMouseDown = false;
            _isDragging = false;
        }

        protected virtual void WinTitleBar_OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_isDragging)
            {
                return;
            }

            if (this.WindowState == WindowState.Maximized)
            {
                var p = ScreenInfoEx.GetMouseVirtualPosition();
                var top = p.Y;
                var left = p.X;
                this.Top = top - 15;
                this.Left = left - this.Width / 2;
                this.WindowState = WindowState.Normal;
                this.Top = top - 15;
                this.Left = left - this.Width / 2;
            }

            try
            {
                this.DragMove();
            }
            catch
            {
                // ignored
            }
        }

        #endregion DragMove
    }
}