using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace _1RM.Service.WebUi
{
    /// <summary>进程内本地 Web 服务：仅监听回环地址。DEBUG 固定端口免 token，Release 随机端口 + token。</summary>
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
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); // 避免与 SimpleLogHelper 重复
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, Port));
            var app = builder.Build();
            app.UseMiddleware<TokenMiddleware>(Token);
            WebUiEndpoints.MapAll(app);
            _app = app;
            _ = app.RunAsync(); // 后台运行，不阻塞 WPF 启动
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
