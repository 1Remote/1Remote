using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PRM.Model;
using PRM.Model.Protocol.Base;
using PRM.Utils;
using PRM.View;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Shawn.Utils.Wpf.Controls;
using Ui;
using Application = System.Windows.Application;

namespace PRM.Service
{
    public class TaskTrayService
    {
        public TaskTrayService()
        {
            IoC.Get<GlobalData>().VmItemListDataChanged += ReloadTaskTrayContextMenu;
        }

        private void ProtocolBaseOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProtocolBase.LastConnTime))
            {
                if (IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray)
                    ReloadTaskTrayContextMenu();
            }
        }

        ~TaskTrayService()
        {
            TaskTrayDispose();
        }

        #region TaskTray

        private System.Windows.Forms.NotifyIcon? _taskTrayIcon = null;
        public void TaskTrayInit()
        {
            Debug.Assert(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico"))?.Stream != null);
            var icon = new System.Drawing.Icon(Application.GetResourceStream(ResourceUriHelper.GetUriFromCurrentAssembly("LOGO.ico")).Stream);
            Debug.Assert(icon != null);
            Executor.OnUIThread(() =>
            {
                _taskTrayIcon ??= new System.Windows.Forms.NotifyIcon
                {
                    Text = Assert.APP_DISPLAY_NAME,
                    BalloonTipText = "",
                    Icon = icon,
                    Visible = true
                };
                _taskTrayIcon.Visible = false;
                ReloadTaskTrayContextMenu();
                GlobalEventHelper.OnLanguageChanged -= ReloadTaskTrayContextMenu;
                GlobalEventHelper.OnLanguageChanged += ReloadTaskTrayContextMenu;
                _taskTrayIcon.MouseClick -= TaskTrayIconOnMouseDoubleClick;
                _taskTrayIcon.MouseClick += TaskTrayIconOnMouseDoubleClick;
                _taskTrayIcon.Visible = true;
            });
        }

        private void TaskTrayIconOnMouseDoubleClick(object? sender, MouseEventArgs e)
        {
            lock (this)
            {
                if (_taskTrayIcon?.Visible == true
                    && e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    IoC.Get<MainWindowViewModel>()?.ShowMe();
                }
            }
        }

        public void TaskTrayDispose()
        {
            if (_taskTrayIcon != null)
            {
                _taskTrayIcon.Visible = false;
                _taskTrayIcon.Dispose();
                _taskTrayIcon = null;
            }
        }


        private void ReloadTaskTrayContextMenu()
        {
            foreach (var protocolBase in IoC.Get<GlobalData>().VmItemList.Select(x => x.Server))
            {
                protocolBase.PropertyChanged -= ProtocolBaseOnPropertyChanged;
                protocolBase.PropertyChanged += ProtocolBaseOnPropertyChanged;
            }

            // rebuild TaskTrayContextMenu while language changed
            if (_taskTrayIcon == null) return;

            var about = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<LanguageService>().Translate("About") + $" {Assert.APP_DISPLAY_NAME}");
            about.Click += (sender, args) =>
            {
                //HyperlinkHelper.OpenUriBySystem("https://github.com/1Remote/1Remote");
                IoC.Get<MainWindowViewModel>().ShowMe(true);
                IoC.Get<MainWindowViewModel>().CmdGoAboutPage.Execute();
            };
            var linkHowToUse = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("about_page_how_to_use"));
            linkHowToUse.Click += (sender, args) =>
            {
                HyperlinkHelper.OpenUriBySystem("https://1remote.github.io/usage/quick-start/");
            };
            var linkFeedback = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("about_page_feedback"));
            linkFeedback.Click += (sender, args) =>
            {
                HyperlinkHelper.OpenUriBySystem("https://github.com/1Remote/1Remote/issues");
            };
            var exit = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<ILanguageService>().Translate("Exit"));
            exit.Click += (sender, args) =>
            {
                lock (this)
                {
                    MsAppCenterHelper.TraceAppStatus(false);
                    TaskTrayDispose();
                    App.Close();
                }
            };

            _taskTrayIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            if (IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray)
            {
                var protocolBaseViewModels = IoC.Get<GlobalData>().VmItemList.OrderByDescending(x => x.Server.LastConnTime).Take(20).ToArray();
                if (protocolBaseViewModels.Any())
                {
                    for (var i = 0; i < 20 && i < protocolBaseViewModels.Length; i++)
                    {
                        var protocolBaseViewModel = protocolBaseViewModels[i];
                        //var text = $"{(i + 1):D2}. {protocolBaseViewModel.Server.DisplayName} - {protocolBaseViewModel.Server.SubTitle}";
                        var text = $"{protocolBaseViewModel.Server.DisplayName}";
                        if (text.Length > 30)
                        {
                            text = text.Substring(0, 30) + "...";
                        }

                        var button = new System.Windows.Forms.ToolStripMenuItem(text);
                        button.Click += (sender, args) => { GlobalEventHelper.OnRequestServerConnect?.Invoke(protocolBaseViewModel.Id, from: "Tray"); };
                        _taskTrayIcon.ContextMenuStrip.Items.Add(button);
                    }
                }
            }




            if (IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray == false)
            {
                var item = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<LanguageService>().Translate("Show recently used here"));
                item.Click += (sender, args) =>
                {
                    IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray = true;
                    IoC.Get<ConfigurationService>().Save();
                    ReloadTaskTrayContextMenu();
                    MsAppCenterHelper.TraceSpecial("Tray recently", "Show");
                };
                _taskTrayIcon.ContextMenuStrip.Items.Add(item);
            }
            else
            {
                if (_taskTrayIcon.ContextMenuStrip.Items.Count > 0)
                    _taskTrayIcon.ContextMenuStrip.Items.Add("-");

                var item = new System.Windows.Forms.ToolStripMenuItem(IoC.Get<LanguageService>().Translate("Hide recently used"));
                item.Click += (sender, args) =>
                {
                    IoC.Get<ConfigurationService>().General.ShowRecentlySessionInTray = false;
                    IoC.Get<ConfigurationService>().Save();
                    ReloadTaskTrayContextMenu();
                    MsAppCenterHelper.TraceSpecial("Tray recently", "Hide");
                };
                _taskTrayIcon.ContextMenuStrip.Items.Add(item);
            }


            //_taskTrayIcon.ContextMenuStrip.Items.Add(linkHowToUse);
            _taskTrayIcon.ContextMenuStrip.Items.Add(linkFeedback);
            _taskTrayIcon.ContextMenuStrip.Items.Add(about);
            _taskTrayIcon.ContextMenuStrip.Items.Add(exit);

            // After startup and initializing our application and when closing our window and minimize the application to tray we free memory with the following line:
            System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet = System.Diagnostics.Process.GetCurrentProcess().MinWorkingSet;
        }
        #endregion

    }
}
