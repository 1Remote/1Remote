using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shawn.Utils.Wpf.Image;
using _1RM.Model;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Resources.Icons;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.View;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// WebUiEndpoints 分域：标签域（聚合/置顶走 GlobalData 与 WebUiSettingsService）。
    /// ─ GET    /api/tags          全部数据源聚合（含计数与置顶状态）
    /// ─ GET    /api/tags/manage   单数据源管理列表
    /// ─ PUT    /api/tags/manage   置顶切换
    /// ─ POST   /api/tags/rename   重命名（联动引用服务器）
    /// ─ DELETE /api/tags/{name}   删除（联动引用服务器）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        internal static void MapTagsList(WebApplication app)
        {
            // 标签聚合（含计数与置顶状态）
            app.MapGet("/api/tags", () =>
            {
                var gd = IoC.Get<GlobalData>();
                return Results.Json(DtoMapper.BuildTags(gd));
            });
        }

        internal static void MapTagsManage(WebApplication app)
        {
            // 标签管理（Plan 3 Task 2）：列表（该数据源聚合）/ 置顶 / 重命名 / 删除。
            // rename/delete 复刻 TagActionHelper 的核心循环（详见 WebUiSettingsService）。
            app.MapGet("/api/tags/manage", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                return Results.Json(WebUiSettingsService.ListManageTags(dataSource));
            });

            app.MapPut("/api/tags/manage", (TagPinRequest? body) =>
            {
                var result = WebUiSettingsService.SetTagPinned(body?.Ds, body?.Name, body?.Pinned);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.Json(result.Item),
                    TagManageStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            app.MapPost("/api/tags/rename", (TagRenameRequest? body) =>
            {
                var result = WebUiSettingsService.RenameTag(body?.Ds, body?.From, body?.To);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.Json(result.Rename),
                    TagManageStatus.NotFound => Results.NotFound(),
                    TagManageStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });

            app.MapDelete("/api/tags/{name}", (string name, string? ds) =>
            {
                var result = WebUiSettingsService.DeleteTag(ds, name);
                return result.Status switch
                {
                    TagManageStatus.Ok => Results.NoContent(),
                    TagManageStatus.NotFound => Results.NotFound(),
                    TagManageStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });
        }
    }
}
