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
using PRM.ViewModel.Configuration;
using Shawn.Utils;

namespace PRM.ViewModel
{
    public class TagFilter
    {
        public TagFilter(string tagName, bool isNegative)
        {
            TagName = tagName;
            IsNegative = isNegative;
        }

        public string TagName { get; }
        public bool IsNegative { get; }

        public override string ToString()
        {
            return TagName + (IsNegative ? VmServerListPage.TagTypeSeparator + "Negative" : "");
        }
    }

    public partial class VmServerListPage : NotifyPropertyChangedBase
    {
        public const string TabAllName = "";
        public const string TabTagsListName = "tags_selector_for_list@#@1__()!";
        public const string TabNoneSelected = "@#@$*%&@!_)@#(&*&!@^$(*&@^*&$^1";
        public const string TagSeparator = "__|||__";
        public const string TagTypeSeparator = "===!|!===";

        private string _selectedTabName = "";
        public string SelectedTabName
        {
            get => _selectedTabName;
            set
            {
                if (_selectedTabName == value) return;
                //MainWindowServerFilter = "";
                if (SetAndNotifyIfChanged(ref _selectedTabName, value))
                {
                    Context.LocalityService.MainWindowTabSelected = value;
                    CalcVisible();
                    RaisePropertyChanged(nameof(TagFilters));
                }
            }
        }

        private ObservableCollection<Tag> _tags = new ObservableCollection<Tag>();
        /// <summary>
        /// list all tags of servers
        /// </summary>
        public ObservableCollection<Tag> Tags
        {
            get => _tags;
            private set => SetAndNotifyIfChanged(ref _tags, value);
        }

        private void ReadTagsFromServers()
        {
            var pinnedTags = Context.ConfigurationService.PinnedTags;
            // set pinned
            // TODO del after 2022.05.31
            if (pinnedTags.Count == 0)
            {
                var allExistedTags = Tag.GetPinnedTags();
                pinnedTags = allExistedTags.Where(x => x.Value == true).Select(x => x.Key).ToList();
            }


            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in ServerListItems.Select(x => x.Server.Tags))
            {
                if (tagNames == null)
                    continue;
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, pinnedTags.Contains(tagName), () =>
                        {
                            Context.ConfigurationService.PinnedTags = Tags.Where(x => x.IsPinned).Select(x => x.Name).ToList();
                            Context.ConfigurationService.Save();
                        })
                        { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            var selectedTagName = this.SelectedTabName;
            Tags = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
            Context.AppData.TagNames = tags.Select(x => x.Name).ToList();
            if (Tags.All(x => x.Name != selectedTagName))
            {
                SelectedTabName = "";
            }
            else
            {
                SelectedTabName = selectedTagName;
            }
        }



        [NotNull]
        public List<TagFilter> TagFilters
        {
            get
            {
                var ret = new List<TagFilter>();
                if (SelectedTabName == VmServerListPage.TabAllName
                    || SelectedTabName == VmServerListPage.TabTagsListName
                    || SelectedTabName == VmServerListPage.TabNoneSelected)
                    return ret;
                var tags = SelectedTabName.Split(new string[] { TagSeparator }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tmp in tags)
                {
                    var strings = tmp.Split(new string[] { TagTypeSeparator }, StringSplitOptions.RemoveEmptyEntries);
                    if (strings.Length > 0
                    && Tags.Any(x => x.Name == strings[0].Trim()))
                    {
                        ret.Add(new TagFilter(strings[0], strings.Length > 1));
                    }
                }
                return ret;
            }
        }

        private void SetSelectedTabName(List<TagFilter> tagFilters)
        {
            var sts = tagFilters.Select(x => x.ToString());
            SelectedTabName = string.Join(TagSeparator, sts);
        }


        #region Tag filter control

        enum FilterTagsControlAction
        {
            Append,
            AppendNegativeFilter,
            Remove,
            Set,
        }
        private void FilterTagsControl(object o, FilterTagsControlAction action)
        {
            if (o == null)
                return;

            string newTagName = string.Empty;
            if (o is Tag obj
                && Tags.Any(x => x.Name == obj.Name))
            {
                newTagName = obj.Name;
            }
            else if (o is string str
                     && Tags.Any(x => x.Name == str))
            {
                newTagName = str;
            }

            if (string.IsNullOrEmpty(newTagName) == false)
            {
                var filters = TagFilters;
                var existed = filters.FirstOrDefault(x => x.TagName == newTagName);
                // remove action
                if (action == FilterTagsControlAction.Remove)
                {
                    if (existed != null)
                    {
                        filters.Remove(existed);
                        SetSelectedTabName(filters);
                    }
                }
                // append action
                else if (action == FilterTagsControlAction.Append
                         || action == FilterTagsControlAction.AppendNegativeFilter)
                {
                    bool isNegative = action == FilterTagsControlAction.AppendNegativeFilter;
                    if (existed == null)
                    {
                        filters.Add(new TagFilter(newTagName, isNegative));
                        SetSelectedTabName(filters);
                    }
                    if (existed != null && existed.IsNegative != isNegative)
                    {
                        filters.Remove(existed);
                        filters.Add(new TagFilter(newTagName, isNegative));
                        SetSelectedTabName(filters);
                    }
                }
                // append action
                else if (action == FilterTagsControlAction.AppendNegativeFilter)
                {
                    if (existed == null)
                    {
                        filters.Add(new TagFilter(newTagName, true));
                        SetSelectedTabName(filters);
                    }
                    if (existed == null)
                    {
                        filters.Add(new TagFilter(newTagName, true));
                        SetSelectedTabName(filters);
                    }
                }
                // set action
                else
                {
                    SelectedTabName = newTagName;
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
                    FilterTagsControl(o, isCtrlDown ? FilterTagsControlAction.Append : FilterTagsControlAction.Set);
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
                    FilterTagsControl(o, FilterTagsControlAction.Remove);
                });
            }
        }


        private RelayCommand _cmdTagSelectWithRightRemove;
        public RelayCommand CmdTagSelectWithRightRemove
        {
            get
            {
                return _cmdTagSelectWithRightRemove ??= new RelayCommand((o) =>
                {
                    FilterTagsControl(o, FilterTagsControlAction.AppendNegativeFilter);
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
                    var obj = o as Tag;
                    if (obj == null || MessageBox.Show(Context.LanguageService.Translate("confirm_to_delete"), Context.LanguageService.Translate("messagebox_title_warning"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.None) == MessageBoxResult.No)
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
                    if (t == obj.Name)
                        t = newTag;
                    if (string.IsNullOrEmpty(newTag) || obj.Name == newTag)
                        return;
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
                        SelectedTabName = t;
                    }

                    // restore display scene
                    if (Tags.Any(x => x.Name == newTag))
                    {
                        Tags.First(x => x.Name == newTag).IsPinned = obj.IsPinned;
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