using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using PRM.Controls;
using PRM.Model;
using PRM.Model.Protocol.Base;
using PRM.Utils;
using Shawn.Utils;
using Shawn.Utils.Interface;
using Shawn.Utils.Wpf;

namespace PRM.View
{

    public partial class ServerListPageViewModel
    {
        public const string TAB_ALL_NAME = "";
        public const string TAB_TAGS_LIST_NAME = "tags_selector_for_list@#@1__()!";
        public const string TAB_NONE_SELECTED = "@#@$*%&@!_)@#(&*&!@^$(*&@^*&$^1";

        public string SelectedTabName { get; private set; } = TAB_ALL_NAME;
        private List<TagFilter> _tagFilters = new List<TagFilter>();
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set
            {
                if (SetAndNotifyIfChanged(ref _tagFilters, value))
                {
                    string tagName;
                    if (_tagFilters?.Count == 1)
                    {
                        tagName = _tagFilters.First().TagName;
                    }
                    else if (_tagFilters == null || _tagFilters?.Count == 0)
                    {
                        tagName = TAB_ALL_NAME;
                    }
                    else // if (filters?.Count > 1)
                    {
                        tagName = TAB_NONE_SELECTED;
                    }
                    if (SelectedTabName == tagName) return;
                    SelectedTabName = tagName;
                    RaisePropertyChanged(nameof(SelectedTabName));
                }
            }
        }


        #region Tag filter control

        /// <summary>
        /// 控制 tag 选择器，新增或删除 tag 过滤器。
        /// </summary>
        /// <param name="o"></param>
        /// <param name="action"></param>
        private void FilterTagsControl(object? o, TagFilter.FilterTagsControlAction action)
        {
            if (o == null)
                return;
            if (Context?.DataService == null) return;
            string newTagName = string.Empty;
            if (o is Tag obj && AppData.TagList.Any(x => x.Name == obj.Name))
            {
                newTagName = obj.Name;
            }
            else if (o is string str && AppData.TagList.Any(x => x.Name == str))
            {
                newTagName = str;
            }

            if (string.IsNullOrEmpty(newTagName) == false)
            {
                var filters = TagFilters.ToList();
                var existed = filters.FirstOrDefault(x => x.TagName == newTagName);
                // remove action
                if (action == TagFilter.FilterTagsControlAction.Remove)
                {
                    if (existed != null)
                    {
                        filters.Remove(existed);
                    }
                }
                // append action
                else if (action == TagFilter.FilterTagsControlAction.AppendIncludedFilter
                         || action == TagFilter.FilterTagsControlAction.AppendExcludedFilter)
                {
                    bool isExcluded = action == TagFilter.FilterTagsControlAction.AppendExcludedFilter;
                    if (existed == null)
                    {
                        filters.Add(TagFilter.Create(newTagName, isExcluded ? TagFilter.FilterType.Excluded : TagFilter.FilterType.Included));
                    }
                    if (existed != null && existed.IsExcluded != isExcluded)
                    {
                        filters.Remove(existed);
                        filters.Add(TagFilter.Create(newTagName, isExcluded ? TagFilter.FilterType.Excluded : TagFilter.FilterType.Included));
                    }
                }
                // set action
                else
                {
                    filters.Clear();
                    filters.Add(TagFilter.Create(newTagName, TagFilter.FilterType.Included));
                }

                TagFilters = filters;
                IoC.Get<MainWindowViewModel>().SetMainFilterString(TagFilters, TagAndKeywordEncodeHelper.DecodeKeyword(_filterString).Item2);
            }
        }


        private RelayCommand? _cmdTagAddIncluded;
        public RelayCommand CmdTagAddIncluded
        {
            get
            {
                return _cmdTagAddIncluded ??= new RelayCommand((o) =>
                {
                    var isCtrlDown = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
                    FilterTagsControl(o, isCtrlDown ? TagFilter.FilterTagsControlAction.AppendIncludedFilter : TagFilter.FilterTagsControlAction.Set);
                });
            }
        }


        private RelayCommand? _cmdTagRemove;
        public RelayCommand CmdTagRemove
        {
            get
            {
                return _cmdTagRemove ??= new RelayCommand((o) =>
                {
                    FilterTagsControl(o, TagFilter.FilterTagsControlAction.Remove);
                });
            }
        }


        private RelayCommand? _cmdTagAddExcluded;
        public RelayCommand CmdTagAddExcluded
        {
            get
            {
                return _cmdTagAddExcluded ??= new RelayCommand((o) =>
                {
                    FilterTagsControl(o, TagFilter.FilterTagsControlAction.AppendExcludedFilter);
                });
            }
        }

        #endregion


        private RelayCommand? _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    if (Context?.DataService == null) return;
                    if (o is not Tag obj || false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete")))
                        return;

                    var protocolServerBases = AppData.VmItemList.Select(x => x.Server);
                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(obj.Name))
                        {
                            server.Tags.Remove(obj.Name);
                        }
                    }
                    AppData.UpdateServer(protocolServerBases);
                    var delete = TagFilters.FirstOrDefault(x => x.TagName == obj.Name);
                    if (delete != null)
                    {
                        var tmp = TagFilters.ToList();
                        tmp.Remove(delete);
                        TagFilters = new List<TagFilter>(tmp);
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
                    if (Context?.DataService == null) return;
                    var obj = o as Tag;
                    if (obj == null)
                        return;
                    string oldTagName = obj.Name;
                    string newTagName = InputWindow.InputBox(IoC.Get<ILanguageService>().Translate("Tags"), IoC.Get<ILanguageService>().Translate("Tags"), obj.Name);
                    newTagName = TagAndKeywordEncodeHelper.RectifyTagName(newTagName);
                    if (string.IsNullOrEmpty(newTagName) || oldTagName == newTagName)
                        return;

                    var protocolServerBases = AppData.VmItemList.Select(x => x.Server) ?? new List<ProtocolBase>();
                    foreach (var server in protocolServerBases)
                    {
                        if (server.Tags.Contains(oldTagName))
                        {
                            server.Tags.Remove(oldTagName);
                            server.Tags.Add(newTagName);
                        }
                    }
                    AppData.UpdateServer(protocolServerBases);


                    // restore selected scene
                    var rename = TagFilters.FirstOrDefault(x => x.TagName == oldTagName);
                    if (rename != null)
                    {
                        var renamed = TagFilter.Create(newTagName, rename.Type);
                        var tmp = TagFilters.ToList();
                        tmp.Remove(rename);
                        tmp.Add(renamed);
                        TagFilters = new List<TagFilter>(tmp);
                    }

                    // restore display scene
                    if (AppData.TagList.Any(x => x.Name == newTagName))
                    {
                        AppData.TagList.First(x => x.Name == newTagName).IsPinned = obj.IsPinned;
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
                    if (Context?.DataService == null) return;
                    if (!(o is Tag obj))
                        return;
                    foreach (var vmProtocolServer in AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id);
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
                    if (Context?.DataService == null) return;
                    if (!(o is Tag obj))
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in AppData.VmItemList.ToArray())
                    {
                        if (vmProtocolServer.Server.Tags.Contains(obj.Name))
                        {
                            GlobalEventHelper.OnRequestServerConnect?.Invoke(vmProtocolServer.Id, token);
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }
    }
}