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
    /// WebUiEndpoints 分域：数据源域（按名寻址，Local 为内置 SQLite 不暴露增删路径）。
    /// ─ GET    /api/datasources             列表（本地 + 附加，config 视图无密码）
    /// ─ POST   /api/datasources             新建（WPF-parity：连接失败不回滚）
    /// ─ PUT    /api/datasources/{name}      更新连接参数 + 可选改名（保存后重连）
    /// ─ DELETE /api/datasources/{name}      删除（serverCount>0 未确认 → 409）
    /// ─ POST   /api/datasources/{name}/test 测试连接（已存项按 config 覆盖 / 未保存草稿按 type 构造）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        internal static void MapDataSources(WebApplication app)
        {
            // 数据源列表：本地 + 附加数据源（config = 连接参数视图，无密码）
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

            // 新建数据源（Plan 3 Task 3）：WPF-parity 持久化 = ConfigurationService.AdditionalDataSource
            // .Add + Save() + DataSourceService.AddOrUpdateDataSource（连接失败不回滚，WPF CmdAdd 同款——
            // 返回 201 + 实际 status，前端可先调 /test 验证）。重名 → 409；校验失败 → 400 零写入。
            app.MapPost("/api/datasources", (DataSourceSaveRequest? body) =>
            {
                var result = WebUiDataSourceService.Create(body);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        dataSource = result.Dto,
                        connectError = result.ConnectError,
                    }, statusCode: 201),
                    DataSourceMutationStatus.Conflict => Results.Json(new { errors = result.Errors }, statusCode: 409),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 更新数据源连接参数（+可选改名，batch10 Task B #7）：body.name 非空且异于路径名 = 改名
            // （WPF CmdEdit 弹窗 Name 写 org.DataSourceName 平价）。Local → 400（SQLite 路径不暴露）；
            // 字段缺失 = 保持；password 空 = 保持（Mysql/Pgsql Password setter 收 "" 会清空）；
            // 保存后 AddOrUpdateDataSource 重连；改名重名 → 409。
            app.MapPut("/api/datasources/{name}", (string name, DataSourceConfigRequest? body) =>
            {
                var result = WebUiDataSourceService.Update(name, body?.Config, body?.Name);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        dataSource = result.Dto,
                        connectError = result.ConnectError,
                    }),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    DataSourceMutationStatus.Conflict => Results.Json(new { errors = result.Errors }, statusCode: 409),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 删除数据源：Local → 400；serverCount>0 且未确认 → 409 {serverCount}（Web 侧守卫，WPF
            // CmdDelete 无检查直接删）；keepServers=true = 用户已确认，镜像 WPF：移除数据源，
            // 服务器不迁移留在库文件中（重新添加该数据源即可找回）。
            app.MapDelete("/api/datasources/{name}", (string name, bool? keepServers) =>
            {
                var result = WebUiDataSourceService.Delete(name, keepServers == true);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.NoContent(),
                    DataSourceMutationStatus.Conflict => Results.Json(
                        new { error = result.Errors.FirstOrDefault(), serverCount = result.ServerCount }, statusCode: 409),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });

            // 测试连接：sqlite = Database_SelfCheck（含错误详情）；mysql/pgsql = 静态 TestConnection，
            // config 未带密码时沿用已存密码。带 config = 按 config 测试（保存前验证向导流程）；
            // 未保存草稿（name 无已存实例）带 body.type = 草稿测试（WPF 测试按钮对表单草稿
            // 构造临时配置测试的平价，batch10 Task B #8）。
            app.MapPost("/api/datasources/{name}/test", (string name, DataSourceConfigRequest? body) =>
            {
                var result = WebUiDataSourceService.Test(name, body?.Config, body?.Type);
                return result.Status switch
                {
                    DataSourceMutationStatus.Ok => Results.Json(new
                    {
                        ok = result.IsOk,
                        status = result.StatusText,
                        detail = result.Detail,
                    }),
                    DataSourceMutationStatus.NotFound => Results.NotFound(),
                    _ => Results.BadRequest(new { errors = new[] { result.Detail } }),
                };
            });
        }
    }
}
