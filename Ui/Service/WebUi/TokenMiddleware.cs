using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace _1RM.Service.WebUi
{
    /// <summary>Release 形态下校验 token（Bearer 或 ?token=）；token 为空串（DEBUG 形态）时放行。</summary>
    public class TokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _token;

        public TokenMiddleware(RequestDelegate next, string token)
        {
            _next = next;
            _token = token;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 仅守卫 API；静态文件（页面本身经 ?token= 加载后，其资源请求不再携带 token）直接放行
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            if (!string.IsNullOrEmpty(_token))
            {
                var provided = context.Request.Query["token"].ToString();
                if (string.IsNullOrEmpty(provided))
                {
                    var auth = context.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                        provided = auth["Bearer ".Length..].Trim();
                }
                var expected = System.Text.Encoding.UTF8.GetBytes(_token);
                var actual = System.Text.Encoding.UTF8.GetBytes(provided ?? string.Empty);
                if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await _next(context);
        }
    }
}
