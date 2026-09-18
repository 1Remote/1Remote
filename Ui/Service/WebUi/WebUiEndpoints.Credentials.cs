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
    /// WebUiEndpoints 分域：凭据域（按名寻址，挂在数据源上，?ds= 缺省 Local）。
    /// ─ GET    /api/credentials/names       名称清单（供编辑器「继承凭据」下拉）
    /// ─ GET    /api/credentials             列表（含引用计数，绝不含密码/私钥）
    /// ─ POST   /api/credentials             新建
    /// ─ PUT    /api/credentials/{name}      更新（整体替换，联动引用服务器改名/同步）
    /// ─ DELETE /api/credentials/{name}      删除（联动清空引用）
    /// ─ POST   /api/credentials/{name}/reveal 明文查看（本地二次验证门）
    /// 注册顺序由主文件 MapAll 统一编排（与拆分前一致）。
    /// </summary>
    public static partial class WebUiEndpoints
    {
        internal static void MapCredentials(WebApplication app)
        {
            // 凭据库名称列表：供编辑器「继承凭据」下拉。GetCredentials 自带缓存判定
            // （NeedRead 时读库）与 lock(this)（数据源实例锁，与 GlobalData 的锁无关），无需 lock(gd)。
            // 未知数据源 → 404（只读名称清单，读库失败语义与 WPF 一致：状态异常时返回缓存）。
            app.MapGet("/api/credentials/names", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                var names = dataSource.GetCredentials().Select(x => x.Name).ToList();
                return Results.Json(new { names });
            });

            // 凭据库列表（Plan 3 Task 1）：{credentials:[{name,address,port,userName,refCount}]}。
            // 安全红线：绝不包含密码/私钥路径——明文查看只能走 reveal 端点（本地二次验证）。
            // 引用计数按该数据源下的服务器扫描（InheritedCredentialName + AlternateCredentials[].Name）。
            // 未知数据源 → 404（与 /api/credentials/names 语义一致）。
            app.MapGet("/api/credentials", (string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var dataSource = IoC.Get<DataSourceService>().GetDataSource(dataSourceName);
                if (dataSource == null)
                    return Results.NotFound();
                return Results.Json(new { credentials = WebUiCredentialService.List(dataSource) });
            });

            // 新建凭据：body {ds?, credential:{Name*,Address,Port,UserName,Password,PrivateKeyPath}}。
            // credential 域 Password/PrivateKeyPath 为明文——加密由 Database_InsertCredential 在内部
            // 克隆上完成；Name 非空+唯一（忽略大小写，WPF 编辑器同款）+长度≤100，违例 400 {errors}；
            // 只读数据源前置 400（InsertCredential 对只读库静默返回 Success，必须先查 IsWritable）。
            app.MapPost("/api/credentials", (CredentialSaveRequest? body) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(body?.Ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : body!.Ds!;
                return MapCredentialSaveResult(WebUiCredentialService.Create(dataSourceName, body?.Credential));
            });

            // 更新凭据（按名寻址，与 WPF 凭据编辑一致）：整体替换语义；Password/PrivateKeyPath
            // 空=保持原值（batch8 #17：编辑 API 不回显明文，空提交不得清空密钥）；
            // nameBefore=路由名驱动引用服务器联动改名/字段同步（Dapper 事务）；
            // 重命名目标名做与新建相同的唯一校验。
            app.MapPut("/api/credentials/{name}", (string name, string? ds, CredentialSaveRequest? body) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                return MapCredentialSaveResult(WebUiCredentialService.Update(dataSourceName, name, body?.Credential));
            });

            // 删除凭据：引用服务器的 InheritedCredentialName 由 Dapper 事务联动清空（WPF 既有行为）；
            // 成功 204；未知名 → 404；只读 → 400（DeleteCredential 对只读库自身会 Fail，此处前置拦截统一语义）。
            app.MapDelete("/api/credentials/{name}", (string name, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var result = WebUiCredentialService.Delete(dataSourceName, name);
                return result.Status switch
                {
                    EditorSaveStatus.Ok => Results.NoContent(),
                    EditorSaveStatus.NotFound => Results.NotFound(),
                    EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                    _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
                };
            });

            // 明文查看凭据：本地二次验证（未开启时 VerifyAsyncUi 直通 true，与 WPF 一致）→
            // 通过后克隆+解密返回 {password, privateKeyPath}；同一数据源 30s 内免再次验证
            // （服务端静态时间戳）。验证失败/用户取消 → 403；未知名 → 404；未知数据源 → 400。
            // 注意 async 端点直接 await（二次验证是 async Task<bool?>，不能用同步 dispatch 包裹）。
            app.MapPost("/api/credentials/{name}/reveal", async (string name, string? ds) =>
            {
                var dataSourceName = string.IsNullOrWhiteSpace(ds)
                    ? DataSourceService.LOCAL_DATA_SOURCE_NAME
                    : ds;
                var result = await WebUiCredentialService.Reveal(dataSourceName, name);
                return result.Status switch
                {
                    CredentialRevealStatus.Ok => Results.Json(new
                    {
                        password = result.Password,
                        privateKeyPath = result.PrivateKeyPath,
                    }),
                    CredentialRevealStatus.NotFound => Results.NotFound(),
                    CredentialRevealStatus.Forbidden => Results.StatusCode(403),
                    _ => Results.BadRequest(new { errors = result.Errors }),
                };
            });
        }

        /// <summary>
        /// 凭据保存（POST/PUT）结果 → HTTP 映射：与 MapSaveResult 同款分类，
        /// 但 Ok 载荷为凭据名（按名寻址资源）：Ok→200 {name}；其余同上。
        /// </summary>
        private static IResult MapCredentialSaveResult(EditorSaveResult result)
        {
            return result.Status switch
            {
                EditorSaveStatus.Ok => Results.Json(new { name = result.ServerId }),
                EditorSaveStatus.BadRequest => Results.BadRequest(new { errors = result.Errors }),
                EditorSaveStatus.NotFound => Results.NotFound(),
                _ => Results.Json(new { error = result.DbErrorInfo }, statusCode: 500),
            };
        }
    }
}
