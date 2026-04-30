using System.Diagnostics;
using System.Text;
using NetMind.Common.Logging;

namespace NetMind.WebApi.Middleware;

public sealed class ApiCallLoggingMiddleware
{
    private const int MaxLoggedBodyLength = 4000;

    private readonly RequestDelegate _next;

    public ApiCallLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAppLogger logger)
    {
        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        Exception? failure = null;
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var originalResponseBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

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
            var finishedAt = DateTimeOffset.Now;
            context.Response.Body = originalResponseBody;
            var responseBody = await ReadResponseBodyAsync(responseBuffer, context.Response.ContentType);
            await responseBuffer.CopyToAsync(originalResponseBody);

            var properties = new Dictionary<string, object?>
            {
                ["TraceId"] = context.TraceIdentifier,
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path.Value,
                ["Query"] = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty,
                ["RequestBody"] = requestBody,
                ["StatusCode"] = context.Response.StatusCode,
                ["ResponseBody"] = responseBody,
                ["StartedAt"] = startedAt.ToString("O"),
                ["FinishedAt"] = finishedAt.ToString("O"),
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

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0 || request.HasFormContentType)
        {
            return string.Empty;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return Truncate(body);
    }

    private static async Task<string> ReadResponseBodyAsync(MemoryStream responseBuffer, string? contentType)
    {
        responseBuffer.Position = 0;
        if (!IsTextResponse(contentType))
        {
            return $"[已省略非文本响应，长度 {responseBuffer.Length} 字节]";
        }

        using var reader = new StreamReader(responseBuffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        responseBuffer.Position = 0;
        return Truncate(body);
    }

    private static bool IsTextResponse(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxLoggedBodyLength)
        {
            return value;
        }

        return value[..MaxLoggedBodyLength] + $"...[已截断，原长度 {value.Length}]";
    }
}
