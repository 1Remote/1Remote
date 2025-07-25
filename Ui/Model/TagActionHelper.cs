using System;
using System.Collections.Generic;
using System.Linq;
using _1RM.Service;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.Utils.Tracing;
using _1RM.View;
using _1RM.View.ServerList;
using _1RM.View.Utils;
using Shawn.Utils;

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
        // o can be a Tag or a TagFilter or a string(tag name)
        var tagName = GetTagNameFromObject(o);
        var t = IoC.Get<GlobalData>().TagList.FirstOrDefault(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase));
        if (t != null)
        {
            t.IsPinned = !t.IsPinned;
            if (o is TagFilter tagFilter)
            {
                tagFilter.RaiseIsPinned();
            }
        }
    }

    public static void CmdTagDelete(object? o)
    {
        // o can be a Tag or a TagFilter or a string(tag name)
        var tagName = GetTagNameFromObject(o);
        if (string.IsNullOrEmpty(tagName))
            return;

        var protocolServerBases = IoC.Get<GlobalData>().VmItemList.Where(x => x.Server.Tags.Contains(tagName!) && x.IsEditable).Select(x => x.Server).ToArray();

        if (protocolServerBases.Any() != true)
        {
            return;
        }

        if (false == MessageBoxHelper.Confirm(IoC.Translate("confirm_to_delete"), ownerViewModel: IoC.Get<MainWindowViewModel>()))
            return;

        foreach (var server in protocolServerBases)
        {
            if (server.Tags.Contains(tagName!))
            {
                server.Tags.Remove(tagName!);
            }
        }
        IoC.Get<GlobalData>().UpdateServer(protocolServerBases);
        if (IoC.Get<GlobalData>().TagList.Count == 0)
            IoC.Get<ServerListPageViewModel>().CmdShowMainTab?.Execute();
    }

    public static async void CmdTagRename(object? o)
    {
        // o can be a Tag or a TagFilter or a string(tag name)
        try
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

            var newTagName = await InputBoxViewModel.GetValue(IoC.Translate("Tags"), new Func<string, string>((str) =>
            {
                if (string.IsNullOrWhiteSpace(str))
                    return IoC.Translate(LanguageService.CAN_NOT_BE_EMPTY);
                if (str == tagName)
                    return "";
                if (IoC.Get<GlobalData>().TagList.Any(x => x.Name == str))
                    return IoC.Translate(LanguageService.XXX_IS_ALREADY_EXISTED, str);
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

            // 4. update db
            IoC.Get<GlobalData>().UpdateServer(protocolServerBases);
        }
        catch (Exception e)
        {
            SimpleLogHelper.Error(e);
            UnifyTracing.Error(e, new Dictionary<string, string>()
            {
                {"Action", "TagActionHelper.CmdTagRename"}
            });
        }
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
                    ? IoC.Translate("Pin")
                    : IoC.Translate("Unpin"),
                action: () => { CmdTagPin(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Translate("Rename"),
                action: () => { CmdTagRename(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Translate("Connect"),
                action: () => { CmdTagConnect(tagName); }
            ));
        }

        {
            actions.Add(new ProtocolAction(
                actionName: IoC.Translate("Delete"),
                action: () => { CmdTagDelete(tagName); }
            ));
        }


        return actions;
    }
}