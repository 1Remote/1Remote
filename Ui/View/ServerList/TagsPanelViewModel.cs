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
using _1RM.Service.Locality;
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

        private static string? GetTagNameFromObject(object? o)
        {
            var tagName = o switch
            {
                string str => str,
                Tag tag => tag.Name.ToLower(),
                TagFilter tagFilter => tagFilter.TagName.ToLower(),
                _ => null
            };
            return tagName;
        }

        private RelayCommand? _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    var tagName = GetTagNameFromObject(o);
                    if (string.IsNullOrEmpty(tagName) || !LocalityTagService.TagDict.ContainsKey(tagName))
                        return;

                    var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(tagName) && x.IsEditable).Select(x => x.Server).ToArray();

                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    if (false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
                        return;

                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(tagName))
                        {
                            server.Tags.Remove(tagName);
                        }
                    }
                    IoC.Get<GlobalData>().UpdateServer(protocolServerBases, false);
                    IoC.Get<ServerListPageViewModel>().CmdTagRemove?.Execute(tagName);
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
                    var tagName = GetTagNameFromObject(o);
                    if (string.IsNullOrEmpty(tagName) || !LocalityTagService.TagDict.ContainsKey(tagName))
                        return;

                    string oldTagName = tagName;
                    var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(oldTagName) && x.IsEditable).Select(x => x.Server).ToArray();
                    if (protocolServerBases.Any() != true)
                    {
                        return;
                    }

                    var newTagName = InputBoxViewModel.GetValue(IoC.Get<ILanguageService>().Translate("Tags"), new Func<string, string>((str) =>
                    {
                        if (string.IsNullOrWhiteSpace(str))
                            return IoC.Get<ILanguageService>().Translate("Can not be empty!");
                        if (str == tagName)
                            return "";
                        if (IoC.Get<GlobalData>().TagList.Any(x => x.Name == str))
                            return IoC.Get<ILanguageService>().Translate("XXX is already existed!", str);
                        return "";
                    }), defaultResponse: tagName, ownerViewModel: IoC.Get<MainWindowViewModel>());

                    newTagName = TagAndKeywordEncodeHelper.RectifyTagName(newTagName);
                    if (string.IsNullOrEmpty(newTagName) || oldTagName == newTagName)
                        return;

                    // 1. update is pin
                    if (LocalityTagService.TagDict.TryRemove(oldTagName.ToLower(), out var oldTag))
                    {
                        oldTag.Name = newTagName;
                        LocalityTagService.UpdateTag(oldTag);
                    }

                    // 2. update server tags
                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(oldTagName))
                        {
                            var tags = new List<string>(server.Tags);
                            tags.Remove(oldTagName);
                            tags.Add(newTagName);
                            server.Tags = tags;
                        }
                    }

                    // 2.5 update tags
                    {
                        var tag = IoC.Get<GlobalData>().TagList.FirstOrDefault(x => x.Name == oldTagName);
                        if (tag != null) tag.Name = newTagName;
                    }


                    // 3. restore selected scene
                    var tagFilters = new List<TagFilter>(IoC.Get<ServerListPageViewModel>().TagFilters);
                    var rename = tagFilters.FirstOrDefault(x => x.TagName == oldTagName);
                    if (rename != null)
                    {
                        rename.TagName = newTagName;
                    }
                    IoC.Get<ServerListPageViewModel>().TagFilters = tagFilters;
                    IoC.Get<MainWindowViewModel>().SetMainFilterString(tagFilters, TagAndKeywordEncodeHelper.DecodeKeyword(IoC.Get<MainWindowViewModel>().MainFilterString).KeyWords);

                    // 4. update to db and reload tags. not tag reload
                    IoC.Get<GlobalData>().UpdateServer(protocolServerBases, false);
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
                    var tagName = GetTagNameFromObject(o);
                    if (string.IsNullOrEmpty(tagName) || !LocalityTagService.TagDict.ContainsKey(tagName))
                        return;
                    var servers = IoC.Get<GlobalData>().VmItemList
                        .Where(x => x.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase)))
                        .Select(x => x.Server)
                        .ToArray();
                    GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: $"{nameof(MainWindowView)}");
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
                    var tagName = GetTagNameFromObject(o);
                    if (string.IsNullOrEmpty(tagName) || !LocalityTagService.TagDict.ContainsKey(tagName))
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    var servers = IoC.Get<GlobalData>().VmItemList
                        .Where(x => x.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase)))
                        .Select(x => x.Server)
                        .ToArray();
                    GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: $"{nameof(MainWindowView)}", assignTabToken: token);
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
                    var tagName = GetTagNameFromObject(o);
                    var t = IoC.Get<GlobalData>().TagList.FirstOrDefault(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase));
                    if (t != null)
                    {
                        t.IsPinned = !t.IsPinned;
                        IoC.Get<ServerListPageViewModel>().TagFilters = new List<TagFilter>(IoC.Get<ServerListPageViewModel>().TagFilters);
                        if (o is TagFilter tagFilter)
                        {
                            tagFilter.RaiseIsPinned();
                        }
                    }
                });
            }
        }
    }
}
