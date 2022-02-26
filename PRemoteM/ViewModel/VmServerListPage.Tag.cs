using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Annotations;
using PRM.Controls;
using PRM.Core;
using PRM.Core.Helper;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Service;
using PRM.Core.Utils.mRemoteNG;
using PRM.Model;
using PRM.Resources.Icons;
using PRM.Utils.Filters;
using PRM.ViewModel.Configuration;
using Shawn.Utils;

namespace PRM.ViewModel
{

    public partial class VmServerListPage : NotifyPropertyChangedBase
    {
        public const string TabAllName = "";
        public const string TabTagsListName = "tags_selector_for_list@#@1__()!";
        public const string TabNoneSelected = "@#@$*%&@!_)@#(&*&!@^$(*&@^*&$^1";

        private string _selectedTabName = "";
        public string SelectedTabName
        {
            get => _selectedTabName;
            set
            {
                if (Context.AppData.TagListDoInvokeSelectedTabName)
                {
                    if (SetAndNotifyIfChanged(ref _selectedTabName, value))
                    {
                        if (Context.AppData.TagList.Any(x => x.Name == value))
                        {
                            TagFilters = new List<TagFilter>() {TagFilter.Create(value, TagFilter.FilterType.Included)};
                        }
                        else
                        {
                            TagFilters = new List<TagFilter>();
                        }

                        var s = TagAndKeywordFilter.DecodeKeyword(Context.AppData.MainWindowServerFilter);
                        Context.AppData.MainWindowServerFilter = TagAndKeywordFilter.EncodeKeyword(TagFilters, s.Item2);
                    }
                }
                Context.AppData.TagListDoInvokeSelectedTabName = true;
            }
        }

        private void SetSelectedTabName(string name)
        {
            if (_selectedTabName == name) return;
            _selectedTabName = name;
            RaisePropertyChanged(nameof(SelectedTabName));
        }


        private List<TagFilter> _tagFilters = new List<TagFilter>();
        [NotNull]
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }


        #region Tag filter control

        private void FilterTagsControl(object o, TagFilter.FilterTagsControlAction action)
        {
            if (o == null)
                return;

            string newTagName = string.Empty;
            if (o is Tag obj
                && Context.AppData.TagList.Any(x => x.Name == obj.Name))
            {
                newTagName = obj.Name;
            }
            else if (o is string str
                     && Context.AppData.TagList.Any(x => x.Name == str))
            {
                newTagName = str;
            }

            if (string.IsNullOrEmpty(newTagName) == false)
            {
                var filters = TagFilters;
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
                var s = TagAndKeywordFilter.DecodeKeyword(Context.AppData.MainWindowServerFilter);
                Context.AppData.MainWindowServerFilter = TagAndKeywordFilter.EncodeKeyword(TagFilters, s.Item2);

                if (filters.Count == 1)
                {
                    SetSelectedTabName(filters.First().TagName);
                }
                else if (filters.Count == 0)
                {
                    SetSelectedTabName(TabAllName);
                }
                else if (filters.Count == 0)
                {
                    SetSelectedTabName(TabNoneSelected);
                }
            }
        }


        private RelayCommand _cmdTagSelect;
        public RelayCommand CmdTagSelect
        {
            get
            {
                return _cmdTagSelect ??= new RelayCommand((o) =>
                {
                    var isCtrlDown = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
                    FilterTagsControl(o, isCtrlDown ? TagFilter.FilterTagsControlAction.AppendIncludedFilter : TagFilter.FilterTagsControlAction.Set);
                });
            }
        }


        private RelayCommand _cmdTagSelectWithLeftRemove;
        public RelayCommand CmdTagSelectWithLeftRemove
        {
            get
            {
                return _cmdTagSelectWithLeftRemove ??= new RelayCommand((o) =>
                {
                    FilterTagsControl(o, TagFilter.FilterTagsControlAction.Remove);
                });
            }
        }


        private RelayCommand _cmdTagSelectWithRightMouse;
        public RelayCommand CmdTagSelectWithRightMouse
        {
            get
            {
                return _cmdTagSelectWithRightMouse ??= new RelayCommand((o) =>
                {
                    FilterTagsControl(o, TagFilter.FilterTagsControlAction.AppendExcludedFilter);
                });
            }
        }



        private RelayCommand _cmdTagDelete;
        public RelayCommand CmdTagDelete
        {
            get
            {
                return _cmdTagDelete ??= new RelayCommand((o) =>
                {
                    if (!(o is Tag obj) || MessageBox.Show(Context.LanguageService.Translate("confirm_to_delete"), Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) == MessageBoxResult.No)
                        return;
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        var s = vmProtocolServer.Server;
                        if (s.Tags.Contains(obj.Name))
                        {
                            s.Tags.Remove(obj.Name);
                        }

                        Context.AppData.UpdateServer(s, false);
                    }

                    Context.AppData.ReloadServerList();
                    SetSelectedTabName(TabAllName);
                });
            }
        }


        #endregion



        private RelayCommand _cmdTagRename;
        public RelayCommand CmdTagRename
        {
            get
            {
                return _cmdTagRename ??= new RelayCommand((o) =>
                {
                    var t = SelectedTabName;
                    var obj = o as Tag;
                    if (obj == null)
                        return;
                    string newTag = InputWindow.InputBox(Context.LanguageService.Translate("Tags"), Context.LanguageService.Translate("Tags"), obj.Name);
                    newTag = TagAndKeywordFilter.RectifyTagName(newTag);
                    if (t == obj.Name)
                        t = newTag;
                    if (string.IsNullOrEmpty(newTag) || obj.Name == newTag)
                        return;

                    // TODO 出错了！
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        var s = vmProtocolServer.Server;
                        if (s.Tags.Contains(obj.Name))
                        {
                            s.Tags.Remove(obj.Name);
                            s.Tags.Add(newTag);
                        }

                        Context.AppData.UpdateServer(s, false);
                    }

                    Context.AppData.ReloadServerList();

                    // restore selected scene
                    if (SelectedTabName == obj.Name)
                    {
                        SetSelectedTabName(t);
                    }

                    // restore display scene
                    if (Context.AppData.TagList.Any(x => x.Name == newTag))
                    {
                        Context.AppData.TagList.First(x => x.Name == newTag).IsPinned = obj.IsPinned;
                    }
                });
            }
        }



        private RelayCommand _cmdTagConnect;
        public RelayCommand CmdTagConnect
        {
            get
            {
                return _cmdTagConnect ??= new RelayCommand((o) =>
                {
                    if (!(o is Tag obj))
                        return;
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
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



        private RelayCommand _cmdTagConnectToNewTab;
        public RelayCommand CmdTagConnectToNewTab
        {
            get
            {
                return _cmdTagConnectToNewTab ??= new RelayCommand((o) =>
                {
                    if (!(o is Tag obj))
                        return;

                    var token = DateTime.Now.Ticks.ToString();
                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
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