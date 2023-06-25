using Shawn.Utils;
using System;

namespace _1RM.Model
{
    public class ProtocolAction : NotifyPropertyChangedBase
    {
        public string ActionName { get; }

        private readonly Action _action;

        public void Run()
        {
            _action?.Invoke();
        }

        public ProtocolAction(string actionName, Action action)
        {
            ActionName = actionName;
            _action = action;
        }
    }
}
