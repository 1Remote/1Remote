using System;
using System.Collections.Generic;
using System.Linq;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Shawn.Utils.Interface;

namespace _1RM.Model;

public static class TagActionHelper
{
    public static string? GetTagNameFromObject(object? o)
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

    public static void CmdTagPin(object? o)
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
    }

    public static void CmdTagDelete(object? o)
    {
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;

        var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(tagName!) && x.IsEditable).Select(x => x.Server).ToArray();

        if (protocolServerBases.Any() != true)
        {
            return;
        }

        if (false == MessageBoxHelper.Confirm(IoC.Get<ILanguageService>().Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
            return;

        foreach (var server in protocolServerBases)
        {
            if (server.Tags.Contains(tagName!))
            {
                server.Tags.Remove(tagName!);
            }
        }
        IoC.Get<GlobalData>().UpdateServer(protocolServerBases, false);
        IoC.Get<ServerListPageViewModel>().CmdTagRemove?.Execute(tagName!);
    }

    public static void CmdTagRename(object? o)
    {
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;

        string oldTagName = tagName!;
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
        }), defaultResponse: tagName!, ownerViewModel: IoC.Get<MainWindowViewModel>());

        newTagName = TagAndKeywordEncodeHelper.RectifyTagName(newTagName);
        if (string.IsNullOrEmpty(newTagName) || oldTagName == newTagName)
            return;

        // 1. update is pin
        var oldTag = LocalityTagService.GetAndRemoveTag(oldTagName);
        if (oldTag != null)
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
    }

    public static void CmdTagConnect(object? o)
    {
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;
        var servers = IoC.Get<GlobalData>().VmItemList
            .Where(x => x.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase)))
            .Select(x => x.Server)
            .ToArray();
        GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: $"{nameof(MainWindowView)}");
    }

    public static void CmdTagConnectToNewTab(object? o)
    {
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;

        var token = DateTime.Now.Ticks.ToString();
        var servers = IoC.Get<GlobalData>().VmItemList
            .Where(x => x.Server.Tags.Any(x => string.Equals(x, tagName, StringComparison.CurrentCultureIgnoreCase)))
            .Select(x => x.Server)
            .ToArray();
        GlobalEventHelper.OnRequestServersConnect?.Invoke(servers, fromView: $"{nameof(MainWindowView)}", assignTabToken: token);
    }
    public static void CmdCreateDesktopShortcut(object? o)
    {
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;
        AppStartupHelper.InstallDesktopShortcutByTag(tagName!, tagName!);
    }

    public static List<ProtocolAction> GetActions(this Tag tag)
    {
        return GetActions(tag.Name, tag.IsPinned);
    }


    public static List<ProtocolAction> GetActions(string tagName, bool isPinned)
    {
        var actions = new List<ProtocolAction>();

        {
            actions.Add(new ProtocolAction(
                actionName: isPinned == false
                    ? IoC.Get<ILanguageService>().Translate("Pin")
                    : IoC.Get<ILanguageService>().Translate("Unpin"),
                action: () => { CmdTagPin(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Get<ILanguageService>().Translate("Rename"),
                action: () => { CmdTagRename(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Get<ILanguageService>().Translate("Connect"),
                action: () => { CmdTagConnect(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Get<ILanguageService>().Translate("Delete"),
                action: () => { CmdTagDelete(tagName); }
            ));
        }


        return actions;
    }
}