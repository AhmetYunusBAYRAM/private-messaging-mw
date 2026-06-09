using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace PRIVATE.MESSAGING.MW.Filters;

public class OtpRateLimitAttribute : ActionFilterAttribute
{
    private readonly int _maxRequests;
    private readonly int _windowSeconds;

    public OtpRateLimitAttribute(int maxRequests = 5, int windowSeconds = 60)
    {
        _maxRequests = maxRequests;
        _windowSeconds = windowSeconds;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var action = context.ActionDescriptor.DisplayName ?? "unknown";
        var key = $"otp_ratelimit:{ip}:{action}";

        cache.TryGetValue(key, out int count);

        if (count >= _maxRequests)
        {
            context.Result = new ObjectResult(new { message = $"Çok fazla istek gönderildi. Lütfen {_windowSeconds} saniye bekleyin." })
            {
                StatusCode = 429
            };
            return;
        }

        cache.Set(key, count + 1, TimeSpan.FromSeconds(_windowSeconds));
        await next();
    }
}
