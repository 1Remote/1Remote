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
            if (!string.IsNullOrEmpty(_token))
            {
                var provided = context.Request.Query["token"].ToString();
                if (string.IsNullOrEmpty(provided))
                {
                    var auth = context.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
                        provided = auth["Bearer ".Length..].Trim();
                }
                if (provided != _token)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await _next(context);
        }
    }
}
