using System;
using System.Windows.Input;

namespace Shawn.Utils
{
    public class RelayCommand : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (_canExecute != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove
            {
                if (_canExecute != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }

        private Func<object, bool> _canExecute;
        private Action<object> _execute;
        public RelayCommand(Action<object> execute) : this(execute, null)
        {
        }
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter = null)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }
        public void Execute(object parameter = null)
        {
            if (_execute != null && CanExecute(parameter))
            {
                _execute(parameter);
            }
        }
    }
}
