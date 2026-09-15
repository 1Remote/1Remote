using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    public static class WebUiEndpoints
    {
        public static void MapAll(WebApplication app)
        {
            app.MapGet("/api/version", () => Results.Json(new
            {
                version = _1RM.AppVersion.Version,
                api = 1,
            }));

            // 服务器列表（只读快照）：跳过分组头 Dummy 与临时会话
            app.MapGet("/api/servers", () =>
            {
                var gd = IoC.Get<GlobalData>();
                // 快照语义：ReloadAll 原子交换 VmItemList 引用且换出的旧列表不再被修改，
                // 读侧即使不加锁也安全；此锁仅与 GlobalData 自身的 lock(this)（StopTick/StartTick）串行化。
                // 注意：加载数据的 GetServers 锁的是 DataSourceService/DataSourceBase 实例，不是 GlobalData。
                lock (gd)
                {
                    var list = gd.VmItemList
                        .Where(vm => vm.Server is not Dummy && !vm.Server.IsTmpSession())
                        .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime))
                        .ToList();
                    return Results.Json(list); // 先物化快照再序列化，锁内不做 IO
                }
            });

            // 数据源列表：本地 + 附加数据源
            app.MapGet("/api/datasources", () =>
            {
                var dss = IoC.Get<DataSourceService>();
                var all = new List<DataSourceBase>();
                if (dss.LocalDataSource != null)
                {
                    all.Add(dss.LocalDataSource);
                }
                all.AddRange(dss.AdditionalSources.Values);
                return Results.Json(all.Select(DtoMapper.FromDataSource).ToList());
            });

            // 标签聚合（含计数与置顶状态）
            app.MapGet("/api/tags", () =>
            {
                var gd = IoC.Get<GlobalData>();
                return Results.Json(DtoMapper.BuildTags(gd));
            });

            // 搜索：与 WPF 主窗口过滤同一套语义（#tag / 空格分隔多关键字 / 拼音，均服务端执行），
            // 空/空白 q 与主窗口空过滤一致 = 不筛选 = 返回全部；前端不得自实现过滤
            app.MapGet("/api/search", (string? q) =>
            {
                var gd = IoC.Get<GlobalData>();
                List<ProtocolBaseViewModel> source;
                lock (gd)
                {
                    // 快照语义同 /api/servers：锁内仅物化列表，
                    // 匹配在锁外执行（MatchKeywords 内部并行，且不触碰 GlobalData 的锁）
                    source = gd.VmItemList
                        .Where(vm => vm.Server is not Dummy && !vm.Server.IsTmpSession())
                        .ToList();
                }

                var matched = FilterHelpers.MatchServers(source, q);
                return Results.Json(matched
                    .Select(vm => DtoMapper.FromServer(vm.Server, vm.DataSourceName, vm.LastConnectTime))
                    .ToList());
            });
        }
    }
}
