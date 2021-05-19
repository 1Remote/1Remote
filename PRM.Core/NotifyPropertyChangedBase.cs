using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PRM.Core
{
    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        protected bool NotifyPropertyChangedEnabled = true;

        public void SetNotifyPropertyChangedEnabled(bool isEnabled)
        {
            NotifyPropertyChangedEnabled = isEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
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

        protected virtual void SetAndNotifyIfChanged<T>(ref T oldValue, T newValue, [CallerMemberName] string propertyName = null)
        {
            SetAndNotifyIfChanged(propertyName, ref oldValue, newValue);
        }

        #endregion INotifyPropertyChanged
    }
}