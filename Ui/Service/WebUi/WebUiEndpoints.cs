using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
        }
    }
}
