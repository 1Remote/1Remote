using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using _1RM.Controls;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.Utils;
using _1RM.View.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;
using Stylet;

namespace _1RM.View.ServerList
{
    public class TagsPanelViewModel : NotifyPropertyChangedBaseScreen
    {
        public GlobalData GlobalData => IoC.Get<GlobalData>();

        private bool _filterIsFocused = false;
        public bool FilterIsFocused
        {
            get => _filterIsFocused;
            set => SetAndNotifyIfChanged(ref _filterIsFocused, value);
        }

        private readonly DebounceDispatcher _debounceDispatcher = new();
        private string _filterString = "";
        public string FilterString
        {
            get => _filterString;
            set
            {
                // can only be called by the Ui
                if (SetAndNotifyIfChanged(ref _filterString, value))
                {
                    _debounceDispatcher.Debounce(150, (obj) =>
                    {
                        if (_filterString == FilterString)
                        {
                            if (this.View is TagsPanelView v)
                            {
                                Execute.OnUIThread(() => { CollectionViewSource.GetDefaultView(v.ListBoxTags.ItemsSource).Refresh(); });
                            }
                        }
                    });
                }
            }
        }



        private RelayCommand? _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(obj.Name) && x.IsEditable).Select(x => x.Server).ToArray();

                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    if (false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                        return;

                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(obj.Name))
                        {
                            server.Tags.Remove(obj.Name);
                        }
                    }
                    IoC.Get<GlobalData>().UpdateServer(protocolServerBases);


                    var tagFilters = IoC.Get<ServerListPageViewModel>().TagFilters;
                    var delete = tagFilters.FirstOrDefault(x => x.TagName == obj.Name);
                    if (delete != null)
                    {
                        var tmp = tagFilters.ToList();
                        tmp.Remove(delete);
                        IoC.Get<ServerListPageViewModel>().TagFilters = new List<TagFilter>(tmp);
                    }
                });
            }
        }





        private RelayCommand? _cmdTagRename;
        public RelayCommand CmdTagRename
        {
            get
            {
                return _cmdTagRename ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    string oldTagName = obj.Name;

                    var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(oldTagName) && x.IsEditable).Select(x => x.Server).ToArray();

                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    var newTagName = InputBoxViewModel.GetValue(IoC.Get<ILanguageService>().Translate("Tags"), new Func<string, string>((str) =>
                    {
                        if (string.IsNullOrWhiteSpace(str))
                            return IoC.Get<ILanguageService>().Translate("Can not be empty!");
                        if (str == obj.Name)
                            return "";
                        if (IoC.Get<GlobalData>().TagList.Any(x => x.Name == str))
                            return IoC.Get<ILanguageService>().Translate("XXX is already existed!", str);
                        return "";
                    }), defaultResponse: obj.Name, ownerViewModel: IoC.Get<MainWindowViewModel>());

                    if (newTagName == null || string.IsNullOrEmpty(newTagName))
                        return;

                    newTagName = TagAndKeywordEncodeHelper.RectifyTagName(newTagName);
                    if (string.IsNullOrEmpty(newTagName) || oldTagName == newTagName)
                        return;

                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(oldTagName))
                        {
                            server.Tags.Remove(oldTagName);
                            server.Tags.Add(newTagName);
                        }
                    }
                    IoC.Get<GlobalData>().UpdateServer(protocolServerBases);


                    // restore selected scene
                    var tagFilters = IoC.Get<ServerListPageViewModel>().TagFilters;
                    var rename = tagFilters.FirstOrDefault(x => x.TagName == oldTagName);
                    if (rename != null)
                    {
                        var renamed = TagFilter.Create(newTagName, rename.Type);
                        var tmp = tagFilters.ToList();
                        tmp.Remove(rename);
                        tmp.Add(renamed);
                        IoC.Get<ServerListPageViewModel>().TagFilters = new List<TagFilter>(tmp);
                    }

                    // restore display scene
                    if (IoC.Get<GlobalData>().TagList.Any(x => x.Name == newTagName))
                    {
                        IoC.Get<GlobalData>().TagList.First(x => x.Name == newTagName).IsPinned = obj.IsPinned;
                    }
                });
            }
        }



        private RelayCommand? _cmdTagConnect;
        public RelayCommand CmdTagConnect
        {
            get
            {
                return _cmdTagConnect ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    foreach (var vmProtocolServer in IoC.Get<GlobalData>().VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Server, fromView: $"{nameof(MainWindowView)}");
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }



        private RelayCommand? _cmdTagConnectToNewTab;
        public RelayCommand CmdTagConnectToNewTab
        {
            get
            {
                return _cmdTagConnectToNewTab ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in IoC.Get<GlobalData>().VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Server, fromView: $"{nameof(MainWindowView)}", assignTabToken: token);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }



        private RelayCommand? _cmdTagPin;
        public RelayCommand CmdTagPin
        {
            get
            {
                return _cmdTagPin ??= new RelayCommand((o) =>
                {
                    if (o is not Tag obj)
                        return;
                    obj.IsPinned = !obj.IsPinned;
                });
            }
        }
    }
}
