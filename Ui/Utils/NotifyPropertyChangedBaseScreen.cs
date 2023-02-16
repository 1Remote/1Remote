using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Shawn.Utils.WpfResources.Theme.Styles;
using Stylet;

namespace _1RM.Utils
{
    public abstract class NotifyPropertyChangedBaseScreen : Screen, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler? PropertyChanged;

        #region INotifyPropertyChanged

        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }


        public void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (NotifyPropertyChangedEnabled)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetAndNotifyIfChanged<T>(string? propertyName, ref T oldValue, T newValue)
        {
            if (oldValue == null && newValue == null) return false;
            if (oldValue != null && oldValue.Equals(newValue)) return false;
            if (newValue != null && newValue.Equals(oldValue)) return false;
            oldValue = newValue;
            RaisePropertyChanged(propertyName);
            return true;
        }

        protected virtual bool SetAndNotifyIfChanged<T>(ref T oldValue, T newValue, [CallerMemberName] string? propertyName = null)
        {
            return SetAndNotifyIfChanged(propertyName, ref oldValue, newValue);
        }

        public bool CanUiAction()
        {
            return this.View is WindowBase { IsClosing: false };
        }

        #endregion INotifyPropertyChanged
    }
}
