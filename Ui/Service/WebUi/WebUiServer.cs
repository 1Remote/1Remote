using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                // 固定 ContentRoot 到 exe 目录，确保无论从何处启动都能命中同一份产物
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.ClearProviders(); // 避免与 SimpleLogHelper 重复
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, Port));
            var app = builder.Build();
            app.UseDefaultFiles(); // GET / → wwwroot/index.html（Web UI 构建产物入口；须在 UseStaticFiles 前注册）
            app.UseStaticFiles(); // wwwroot 静态资源（/assets/* 等）。TokenMiddleware 仅守卫 /api，静态资源自由通行
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
    }
}
