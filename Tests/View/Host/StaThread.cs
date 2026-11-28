using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Tests.View.Host
{
    // runs code on an STA thread, WPF controls can only be created there
    internal static class StaThread
    {
        public static void Run(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
