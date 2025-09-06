using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.Model;
using _1RM.Utils.Tracing;
using _1RM.View;
using _1RM.View.ServerView;
using Shawn.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace _1RM.Model
{
    public partial class GlobalData : NotifyPropertyChangedBase
    {
        private readonly Timer _timer = new Timer(500);
        private bool _timerStopFlag = false;

        private void InitTimer()
        {
            _timer.AutoReset = false;
            _timer.Elapsed += (sender, args) => TimerOnElapsed();
        }

        public void StopTick()
        {
            lock (this)
            {
                _timer.Stop();
                _timerStopFlag = true;
            }
        }
        public void StartTick()
        {
            CheckUpdateTime = DateTime.Now.AddSeconds(_configurationService.DatabaseCheckPeriod);
            lock (this)
            {
                _timerStopFlag = false;
                if (_timer.Enabled == false && _configurationService.DatabaseCheckPeriod > 0)
                {
                    _timer.Start();
                }
            }
        }

        /// <summary>
        /// return time string like 1d 2h 3m 4s
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        private static string GetTime(long seconds)
        {
            var sb = new StringBuilder();
            if (seconds > 86400)
            {
                sb.Append($"{seconds / 86400}d");
                seconds %= 86400;
            }

            if (seconds > 3600)
            {
                sb.Append($"{seconds / 3600}h");
                seconds %= 3600;
            }

            if (seconds > 60)
            {
                sb.Append($"{seconds / 60}m");
                seconds %= 60;
            }

            if (seconds > 0)
            {
                sb.Append($"{seconds}s");
            }
            return sb.ToString();
        }

        public DateTime CheckUpdateTime;
        private void TimerOnElapsed()
        {
            try
            {
                if (_sourceService == null)
                    return;

                var ds = new List<DataSourceBase>();
                if (_sourceService.LocalDataSource != null)
                    ds.Add(_sourceService.LocalDataSource);
                ds.AddRange(_sourceService.AdditionalSources.Values);

                var mainWindowViewModel = IoC.TryGet<MainWindowViewModel>();
                var listPageViewModel = IoC.TryGet<ServerListPageViewModel>();
                var launcherWindowViewModel = IoC.TryGet<LauncherWindowViewModel>();

                if (mainWindowViewModel == null
                   || listPageViewModel == null
                   || launcherWindowViewModel == null)
                    return;

                // do not reload when any selected / launcher is shown / editor view is show
                if (listPageViewModel.VmServerList.Any(x => x.IsSelected)
                    || launcherWindowViewModel?.View?.IsVisible == true)
                {
                    var pause = IoC.Translate("Pause");
                    foreach (var s in ds)
                    {
                        s.ReconnectInfo = pause;
                    }
                    return;
                }


                long checkUpdateEtc = 0;
                if (CheckUpdateTime > DateTime.Now)
                {
                    var ts = CheckUpdateTime - DateTime.Now;
                    checkUpdateEtc = (long)ts.TotalSeconds;
                }
                long minReconnectEtc = int.MaxValue;


                var needReconnect = new List<DataSourceBase>();
                foreach (var s in ds.Where(x => x.Status != EnumDatabaseStatus.OK))
                {
                    if (s.ReconnectTime > DateTime.Now)
                    {
                        minReconnectEtc = Math.Min((long)(s.ReconnectTime - DateTime.Now).TotalSeconds, minReconnectEtc);
                    }
                    else
                    {
                        minReconnectEtc = 0;
                        needReconnect.Add(s);
                    }
                }

                var minEtc = Math.Min(checkUpdateEtc, minReconnectEtc);


                var msg = minEtc > 0 ? $"{IoC.Translate("Next update check")} {GetTime(minEtc)}" : IoC.Translate("Updating");
                var msgNextReconnect = IoC.Translate("Next auto reconnect");
                var msgReconnecting = IoC.Translate("Reconnecting");
                foreach (var s in ds)
                {
                    if (s.Status != EnumDatabaseStatus.OK)
                    {
                        if (s.ReconnectTime > DateTime.Now)
                        {
                            var seconds = (long)(s.ReconnectTime - DateTime.Now).TotalSeconds;
                            s.ReconnectInfo = $"{msgNextReconnect} {GetTime(seconds)}";
                        }
                        else
                        {
                            s.ReconnectInfo = msgReconnecting;
                        }
                    }
                    else
                    {
                        s.ReconnectInfo = msg;
                    }
                }

                if (minEtc > 0 && minReconnectEtc > 0)
                {
                    return;
                }

                // reconnect 
                foreach (var dataSource in needReconnect.Where(x => x.ReconnectTime < DateTime.Now))
                {
                    if (dataSource.Database_SelfCheck().Status == EnumDatabaseStatus.OK)
                    {
                        minEtc = 0;
                    }
                }

                if (minEtc == 0)
                {
                    if (ReloadAll()) // reload data in the timer
                    {
                        SimpleLogHelper.Debug("check database update - reload data by timer " + _timer.GetHashCode());
                    }
                    else
                    {
                        SimpleLogHelper.Debug("check database update - no need reload by timer " + _timer.GetHashCode());
                    }

                    // TODO: reload credentials
                }

                System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
                CheckUpdateTime = DateTime.Now.AddSeconds(_configurationService.DatabaseCheckPeriod);
            }
            catch (Exception ex)
            {
                UnifyTracing.Error(ex);
                throw;
            }
            finally
            {
                lock (this)
                {
                    if (_timerStopFlag == false && _configurationService.DatabaseCheckPeriod > 0)
                    {
                        _timer.Start();
                    }
                }
            }
        }
    }
}