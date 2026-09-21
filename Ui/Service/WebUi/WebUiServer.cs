using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shawn.Utils;

namespace _1RM.Service.WebUi
{
    /// <summary>
    /// 进程内本地 Web 服务：仅监听回环地址。DEBUG 固定端口免 token，Release 随机端口 + token。
    /// 注意：WPF UI 线程禁止同步（.Result/.Wait）调用本服务端点（与 SessionControlService 关闭路径的 _dictLock+OnUIThreadSync 组合可致死锁）
    /// </summary>
    public static class WebUiServer
    {
        private static WebApplication? _app;
        public static int Port { get; private set; }
        public static string Token { get; private set; } = string.Empty;
        public static bool IsRunning => _app != null;

        public static void Start()
        {
            if (_app != null) return;
#if DEBUG
            Port = 17321;
            Token = string.Empty;
#else
            Port = Random.Shared.Next(18000, 25000);
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
#endif
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                // WPF 进程的工作目录不保证是 exe 目录（快捷方式/开机自启等场景），而静态托管按
                // ContentRoot 解析 wwwroot（csproj 已将 webui/dist 复制到输出目录 wwwroot/），
                // 固定 ContentRoot 到 exe 目录，确保无论从何处启动都能命中同一份产物。
                // 单文件发布（PublishSingleFile+IncludeAllContentForSelfExtract，build-release.cmd
                // 的 single 模式）下 wwwroot 打进 exe、运行时解压到临时目录，exe 旁没有 wwwroot——
                // 此时 ContentRoot 指向解压目录（ResolveContentRoot 探测，见下）
                ContentRootPath = ResolveContentRoot(),
            });
            builder.Logging.ClearProviders(); // 避免与 SimpleLogHelper 重复
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, Port));
            var app = builder.Build();
            app.UseDefaultFiles(); // GET / → wwwroot/index.html（Web UI 构建产物入口；须在 UseStaticFiles 前注册）
            // index.html 禁缓存（2026-09-21：WebView2/浏览器对无 Cache-Control 的响应做启发式缓存，
            // 升级前端后旧 index.html 引用旧哈希资源，出现「已修复却仍复现旧 bug」；带哈希的
            // /assets/* 反向允许长缓存——内容变即换名，天然免失效问题）
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    if (Path.GetFileName(ctx.File.Name) == "index.html")
                        ctx.Context.Response.Headers.CacheControl = "no-cache";
                },
            });
            app.UseMiddleware<TokenMiddleware>(Token);
            WebUiEndpoints.MapAll(app);
            _app = app;
            try
            {
                app.Start(); // 同步绑定——端口被占用时在此处抛出，调用方可捕获
            }
            catch
            {
                _app = null;
                throw;
            }
        }

        public static async Task StopAsync()
        {
            if (_app == null) return;
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        /// <summary>
        /// 静态托管根目录解析（wwwroot 所在的 ContentRoot）：
        /// ① 常规构建/目录发布——exe 旁 wwwroot（AppContext.BaseDirectory，历史行为）；
        /// ② 单文件发布——wwwroot 被 IncludeAllContentForSelfExtract 打进 exe，运行时按需解压到
        ///    {DOTNET_BUNDLE_EXTRACT_BASE_DIR（缺省 %TEMP%\.net）}\{AssemblyName}\ 下（按修改时间
        ///    取最新），AppContext.BaseDirectory 仍是 exe 所在目录，探测不到 wwwroot——回落到解压目录。
        /// 两处都没有（纯桌面引擎、前端从未构建）时返回 exe 目录维持原行为（静态 404 不影响 WPF 引擎）。
        /// internal 便于单测（WebUiServerTests）。
        /// </summary>
        public static string ResolveContentRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            if (Directory.Exists(Path.Combine(baseDir, "wwwroot")))
                return baseDir;

            var extractBase = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
            if (string.IsNullOrWhiteSpace(extractBase))
                extractBase = Path.Combine(Path.GetTempPath(), ".net");
            try
            {
                var candidate = Directory
                    .EnumerateDirectories(extractBase, "1Remote", SearchOption.AllDirectories)
                    .Select(d => Path.Combine(d, "wwwroot"))
                    .Where(Directory.Exists)
                    .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
                    .FirstOrDefault();
                if (candidate != null)
                    return Path.GetDirectoryName(candidate)!;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Warning($"single-file wwwroot probe failed: {e.Message}"); // 探测失败不阻断启动
            }
            return baseDir;
        }
    }
}
