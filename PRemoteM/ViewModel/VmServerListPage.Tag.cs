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
                    if (SetAndNotifyIfChanged(ref _selectedTabName, value) && value != TabTagsListName)
                    {
                        if (Context.AppData.TagList.Any(x => x.Name == value))
                        {
                            TagFilters = new List<TagFilter>() { TagFilter.Create(value, TagFilter.FilterType.Included) };
                        }
                        else
                        {
                            TagFilters = new List<TagFilter>();
                        }
                        var s = TagAndKeywordFilter.DecodeKeyword(_vmMain.FilterString);
                        SetFilterString(TagFilters, s.Item2);
                    }
                }
                Context.AppData.TagListDoInvokeSelectedTabName = true;
            }
        }


        private List<TagFilter> _tagFilters = new List<TagFilter>();
        [NotNull]
        public List<TagFilter> TagFilters
        {
            get => _tagFilters;
            set => SetAndNotifyIfChanged(ref _tagFilters, value);
        }


        #region Tag filter control

        /// <summary>
        /// 控制 tag 选择器，新增或删除 tag 过滤器。
        /// </summary>
        /// <param name="o"></param>
        /// <param name="action"></param>
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
                var s = TagAndKeywordFilter.DecodeKeyword(_vmMain.FilterString);
                SetFilterString(TagFilters, s.Item2);
            }
        }


        private RelayCommand _cmdTagAddIncluded;
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


        private RelayCommand _cmdTagRemove;
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


        private RelayCommand _cmdTagAddExcluded;
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
                    SelectedTabName = TabAllName;
                });
            }
        }





        private RelayCommand _cmdTagRename;
        public RelayCommand CmdTagRename
        {
            get
            {
                return _cmdTagRename ??= new RelayCommand((o) =>
                {
                    var selectedTabName = SelectedTabName;
                    var obj = o as Tag;
                    if (obj == null)
                        return;
                    string newTagName = InputWindow.InputBox(Context.LanguageService.Translate("Tags"), Context.LanguageService.Translate("Tags"), obj.Name);
                    newTagName = TagAndKeywordFilter.RectifyTagName(newTagName);
                    if (string.IsNullOrEmpty(newTagName) || obj.Name == newTagName)
                        return;

                    foreach (var vmProtocolServer in Context.AppData.VmItemList.ToArray())
                    {
                        var s = vmProtocolServer.Server;
                        if (s.Tags.Contains(obj.Name))
                        {
                            s.Tags.Remove(obj.Name);
                            s.Tags.Add(newTagName);
                        }

                        Context.AppData.UpdateServer(s, false);
                    }

                    Context.AppData.ReloadServerList();

                    // restore selected scene
                    if (selectedTabName == obj.Name)
                    {
                        SelectedTabName = newTagName;
                    }

                    // restore display scene
                    if (Context.AppData.TagList.Any(x => x.Name == newTagName))
                    {
                        Context.AppData.TagList.First(x => x.Name == newTagName).IsPinned = obj.IsPinned;
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







        private void SetFilterString(List<TagFilter> filters, List<string> keywords)
        {
            _vmMain.SetFilterStringByBackend(TagAndKeywordFilter.EncodeKeyword(TagFilters, keywords));
            SetSelectedTabName(filters);
        }

        private void SetSelectedTabName(List<TagFilter> filters = null)
        {
            var tagName = TabAllName;
            if (filters?.Count == 1)
            {
                tagName = filters.First().TagName;
            }
            else if (filters == null || filters?.Count == 0)
            {
                tagName = TabAllName;
            }
            else // if (filters?.Count > 1)
            {
                tagName = TabNoneSelected;
            }
            if (_selectedTabName == tagName) return;
            _selectedTabName = tagName;
            RaisePropertyChanged(nameof(SelectedTabName));
        }
    }
}