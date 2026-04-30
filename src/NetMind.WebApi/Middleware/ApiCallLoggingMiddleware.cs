using System.Diagnostics;
using NetMind.Common.Logging;

namespace NetMind.WebApi.Middleware;

public sealed class ApiCallLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ApiCallLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAppLogger logger)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? failure = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var properties = new Dictionary<string, object?>
            {
                ["TraceId"] = context.TraceIdentifier,
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path.Value,
                ["StatusCode"] = context.Response.StatusCode,
                ["ElapsedMs"] = stopwatch.ElapsedMilliseconds
            };

            if (failure is null && context.Response.StatusCode < 500)
            {
                logger.Info("接口调用", "接口调用完成。", properties);
            }
            else if (failure is null)
            {
                logger.Warning("接口调用", "接口调用返回服务端错误。", properties);
            }
            else
            {
                logger.Error("接口调用", failure, "接口调用发生未处理异常。", properties);
            }
        }
    }
}
