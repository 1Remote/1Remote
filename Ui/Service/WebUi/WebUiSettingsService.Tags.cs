using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Shawn.Utils;
using Stylet;
using _1RM.Model;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// WebUiSettingsService 分域：标签管理域（/api/tags*）。
    /// ─ GET    /api/tags/manage?ds=   该数据源的标签聚合列表（name/count/pinned）
    /// ─ PUT    /api/tags/manage       置顶/取消置顶（幂等，目标值语义）
    /// ─ POST   /api/tags/rename       数据源范围内重命名
    /// ─ DELETE /api/tags/{name}?ds=   从该数据源所有服务器移除标签
    ///
    /// rename/delete 复刻 TagActionHelper.CmdTagRename/CmdTagDelete 的核心循环（Tags 列表
    /// ordinal 精确匹配 + 整表替换 + 批量 UpdateServer），范围限定在请求的数据源（WPF 扫全部
    /// VmItemList）；rename 先迁移 LocalityTagService 置顶状态（GetAndRemoveTag + UpdateTag，
    /// pin 随名迁移）；delete 后仅当该标签在所有数据源都不再存在才清理置顶信息（WPF
    /// CmdTagDelete 不清理，属有意补齐——跨数据源共享标签名时保留 pin 才正确）。
    /// </summary>
    public static partial class WebUiSettingsService
    {
        /// <summary>
        /// GET /api/tags/manage?ds=：该数据源下的标签聚合（name=规范化小写 / count=服务器数 /
        /// pinned=LocalityTagService 置顶）。计数语义与 ReloadTagsFromServers 一致（标签 Trim+小写后聚合）。
        /// 排序：置顶优先，再按 locality 自定义序，最后按名。
        /// </summary>
        public static List<TagManageItemDto> ListManageTags(DataSourceBase dataSource)
        {
            var servers = SnapshotServersOfDataSource(dataSource);
            var counts = servers
                .SelectMany(s => s.Tags.Select(t => t.Trim().ToLower()))
                .GroupBy(n => n, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            LocalityTagService.Load();
            return counts
                .Select(kv => new TagManageItemDto
                {
                    Name = kv.Key,
                    Count = kv.Value,
                    Pinned = LocalityTagService.GetIsPinned(kv.Key),
                })
                .OrderByDescending(x => x.Pinned)
                .ThenBy(x => LocalityTagService.GetCustomOrder(x.Name))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// PUT /api/tags/manage：置顶/取消置顶（幂等，目标值语义）。复刻 WPF CmdTagPin：在
        /// GlobalData.TagList 上找 Tag 对象并设置 IsPinned——其 setter 负责 locality 落盘、
        /// 置顶标签移到列表尾并重排 CustomOrder、刷新标签过滤条可见性。
        /// 置顶状态是机器本地（跨数据源共享），不在数据源内校验可写性；未知标签名 → 404。
        /// </summary>
        public static TagManageResult SetTagPinned(string? dataSourceName, string? name, bool? pinned)
        {
            if (pinned == null)
                return TagManageResult.BadRequest("body must contain a 'pinned' boolean");
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");

            var tagName = TagAndKeywordEncodeHelper.RectifyTagName(name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(tagName))
                return TagManageResult.BadRequest("name: can not be empty");

            var gd = IoC.Get<GlobalData>();
            List<TagManageItemDto> listAfter;
            lock (gd)
            {
                var tag = gd.TagList.FirstOrDefault(x => string.Equals(x.Name, tagName, StringComparison.CurrentCultureIgnoreCase));
                if (tag == null)
                    return TagManageResult.NotFound();
                tag.IsPinned = pinned.Value; // Tag setter 内完成 locality 落盘与重排（WPF 同款）
                listAfter = ListManageTagsUnlocked(dataSource, gd);
            }
            // 置顶目标是全局 TagList：标签可能只存在于其它数据源（该 ds 计数为 0），此时聚合列表
            // 查不到条目——回退手工构造（name + 目标 pin 态），避免响应体为 null
            var updated = listAfter.FirstOrDefault(x => x.Name == tagName)
                ?? new TagManageItemDto { Name = tagName, Count = 0, Pinned = pinned.Value };
            return TagManageResult.Ok(updated);
        }

        /// <summary>
        /// POST /api/tags/rename：数据源范围内重命名。校验（与 WPF 输入校验同序：先空白/同名/已存在
        /// 预检再 RectifyTagName 规范化——"  " 规范化后会变成 "--" 故必须前置拦截；to 空/同名/已存在
        /// → 400；from 不在该数据源 → 404；只读数据源 → 400）全通过后按 WPF CmdTagRename 顺序执行：
        /// locality 置顶状态随名迁移 → 服务器 Tags 替换（new List：Remove 旧名 + Add 新名）→
        /// TagList 内联改名 → 批量 UpdateServer（库内生效 + ReloadTagsFromServers 重建 TagList）。
        /// 与 WPF 的差异：WPF 扫全部数据源的可编辑服务器，此处限定请求的 ds（多个数据源共享标签名时
        /// 其它数据源保持旧名，聚合视图会出现新旧两个名字——ds 范围语义，属有意设计）。
        /// </summary>
        public static TagManageResult RenameTag(string? dataSourceName, string? from, string? to)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");
            if (dataSource.IsWritable != true)
                return TagManageResult.BadRequest($"dataSource '{dataSource.DataSourceName}' is read-only");

            // 空白预检在规范化前（RectifyTagName 会把纯空格变成 "-"，WPF 输入校验同序）
            if (string.IsNullOrWhiteSpace(from))
                return TagManageResult.BadRequest("from: can not be empty");
            if (string.IsNullOrWhiteSpace(to))
                return TagManageResult.BadRequest("to: can not be empty");
            var fromName = TagAndKeywordEncodeHelper.RectifyTagName(from);
            var toName = TagAndKeywordEncodeHelper.RectifyTagName(to);
            if (string.IsNullOrEmpty(fromName))
                return TagManageResult.BadRequest("from: can not be empty");
            if (string.IsNullOrEmpty(toName))
                return TagManageResult.BadRequest("to: can not be empty");
            if (fromName == toName)
                return TagManageResult.BadRequest("to: must differ from 'from'");

            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> targets;
            lock (gd)
            {
                // WPF CmdTagRename 核心循环：Tags 列表 ordinal 精确匹配（标签约定小写存储）
                targets = gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server)
                                 && vm.Server.Tags.Contains(fromName))
                    .Select(vm => vm.Server)
                    .ToList();
                if (targets.Count == 0)
                    return TagManageResult.NotFound();
                if (gd.VmItemList.Any(vm => vm.DataSourceName == dataSource.DataSourceName
                                            && WebUiEndpoints.IsConnectable(vm.Server)
                                            && vm.Server.Tags.Contains(toName)))
                {
                    return TagManageResult.BadRequest($"to: tag '{toName}' already exists in dataSource '{dataSource.DataSourceName}'");
                }

                // 1. locality 置顶状态随名迁移（WPF 步骤 1：GetAndRemoveTag + 改名 + UpdateTag）
                var oldTag = LocalityTagService.GetAndRemoveTag(fromName);
                if (oldTag != null)
                {
                    oldTag.Name = toName;
                    LocalityTagService.UpdateTag(oldTag);
                }

                // 2. 服务器 Tags 替换（WPF 步骤 2：整表替换——Remove 旧名 + Add 新名）
                foreach (var server in targets)
                {
                    if (server.Tags.Contains(fromName))
                    {
                        var tags = new List<string>(server.Tags);
                        tags.Remove(fromName);
                        tags.Add(toName);
                        server.Tags = tags;
                    }
                }

                // 2.5 TagList 内联改名（WPF 步骤 2.5；UpdateServer 成功后 ReloadTagsFromServers 也会重建）
                var tag = gd.TagList.FirstOrDefault(x => x.Name == fromName);
                if (tag != null) tag.Name = toName;
            }

            // 3. 落库（lock 外：UpdateServer 内部自有 StopTick/StartTick 加锁，且含 DB IO）
            var ret = gd.UpdateServer(targets);
            if (!ret.IsSuccess)
                return TagManageResult.DbError(ret.ErrorInfo, updated: 0);
            return TagManageResult.Ok(new TagRenameResultDto { From = fromName, To = toName, Updated = targets.Count });
        }

        /// <summary>
        /// DELETE /api/tags/{name}?ds=：从该数据源所有服务器移除标签（WPF CmdTagDelete 核心循环：
        /// Tags 原地 Remove + 批量 UpdateServer）。成功后仅当该标签在所有数据源都不再存在时清理
        /// locality 置顶信息（跨数据源共享名时保留 pin；WPF 自身不清理，属 plan 要求的有意补齐）。
        /// </summary>
        public static TagManageResult DeleteTag(string? dataSourceName, string? name)
        {
            var dataSource = ResolveDataSource(dataSourceName);
            if (dataSource == null)
                return TagManageResult.BadRequest($"unknown dataSourceName '{dataSourceName}'");
            if (dataSource.IsWritable != true)
                return TagManageResult.BadRequest($"dataSource '{dataSource.DataSourceName}' is read-only");

            var tagName = TagAndKeywordEncodeHelper.RectifyTagName(name);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(tagName))
                return TagManageResult.BadRequest("name: can not be empty");

            var gd = IoC.Get<GlobalData>();
            List<ProtocolBase> targets;
            lock (gd)
            {
                targets = gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server)
                                 && vm.Server.Tags.Contains(tagName))
                    .Select(vm => vm.Server)
                    .ToList();
                if (targets.Count == 0)
                    return TagManageResult.NotFound();

                foreach (var server in targets)
                {
                    if (server.Tags.Contains(tagName))
                    {
                        server.Tags.Remove(tagName); // WPF CmdTagDelete：原地移除
                    }
                }
            }

            var ret = gd.UpdateServer(targets);
            if (!ret.IsSuccess)
                return TagManageResult.DbError(ret.ErrorInfo, updated: 0);

            // 置顶信息清理：仅当所有数据源都不再有此标签（否则其它数据源仍引用，保留 pin）
            lock (gd)
            {
                var stillExists = gd.VmItemList
                    .Where(vm => WebUiEndpoints.IsConnectable(vm.Server))
                    .Any(vm => vm.Server.Tags.Contains(tagName));
                if (!stillExists)
                    LocalityTagService.GetAndRemoveTag(tagName);
            }
            return TagManageResult.Ok(updated: targets.Count);
        }

        // ------------------------------------------------------------------
        // 域私有助手
        // ------------------------------------------------------------------

        /// <summary>
        /// 物化某数据源的服务器快照（lock(gd) 内物化、锁外使用——与 /api/servers 快照纪律一致）。
        /// </summary>
        private static List<ProtocolBase> SnapshotServersOfDataSource(DataSourceBase dataSource)
        {
            var gd = IoC.Get<GlobalData>();
            lock (gd)
            {
                return gd.VmItemList
                    .Where(vm => vm.DataSourceName == dataSource.DataSourceName
                                 && WebUiEndpoints.IsConnectable(vm.Server))
                    .Select(vm => vm.Server)
                    .ToList();
            }
        }

        /// <summary>调用方已持 lock(gd) 的标签聚合（SetTagPinned 响应构造用：Tag.IsPinned 已原地更新）。</summary>
        private static List<TagManageItemDto> ListManageTagsUnlocked(DataSourceBase dataSource, GlobalData gd)
        {
            var counts = gd.VmItemList
                .Where(vm => vm.DataSourceName == dataSource.DataSourceName && WebUiEndpoints.IsConnectable(vm.Server))
                .Select(vm => vm.Server)
                .SelectMany(s => s.Tags.Select(t => t.Trim().ToLower()))
                .GroupBy(n => n, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            LocalityTagService.Load();
            return counts
                .Select(kv => new TagManageItemDto
                {
                    Name = kv.Key,
                    Count = kv.Value,
                    Pinned = LocalityTagService.GetIsPinned(kv.Key),
                })
                .OrderByDescending(x => x.Pinned)
                .ThenBy(x => LocalityTagService.GetCustomOrder(x.Name))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>数据源名解析：空白视为 Local；未知返回 null（调用方转 400）。</summary>
        private static DataSourceBase? ResolveDataSource(string? dataSourceName)
        {
            var name = string.IsNullOrWhiteSpace(dataSourceName)
                ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                : dataSourceName;
            return IoC.Get<DataSourceService>().GetDataSource(name);
        }
    }

    /// <summary>标签管理（pin/rename/delete）结果分类，由端点映射为 HTTP 状态码。</summary>
    public enum TagManageStatus
    {
        Ok,
        BadRequest, // 参数非法/未知数据源/只读数据源/重名目标
        NotFound,   // 标签在该数据源不存在
        DbError,    // 批量 UpdateServer 失败
    }

    public sealed class TagManageResult
    {
        public TagManageStatus Status { get; private init; }
        public List<string> Errors { get; private init; } = new();
        public string DbErrorInfo { get; private init; } = string.Empty;
        public TagManageItemDto? Item { get; private init; }
        public TagRenameResultDto? Rename { get; private init; }
        public int Updated { get; private init; }

        public static TagManageResult Ok(TagManageItemDto item) => new() { Status = TagManageStatus.Ok, Item = item };
        public static TagManageResult Ok(TagRenameResultDto rename) => new() { Status = TagManageStatus.Ok, Rename = rename, Updated = rename.Updated };
        public static TagManageResult Ok(int updated) => new() { Status = TagManageStatus.Ok, Updated = updated };
        public static TagManageResult NotFound() => new() { Status = TagManageStatus.NotFound };
        public static TagManageResult BadRequest(params string[] errors) => new() { Status = TagManageStatus.BadRequest, Errors = errors.ToList() };
        public static TagManageResult DbError(string errorInfo, int updated) => new() { Status = TagManageStatus.DbError, DbErrorInfo = errorInfo, Updated = updated };
    }

    /// <summary>POST /api/tags/rename 成功载荷：{from, to, updated}。</summary>
    public class TagRenameResultDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public int Updated { get; set; }
    }
}
