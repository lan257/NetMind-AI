using NetMind.Common.Logging;

namespace NetMind.WebApi.Infrastructure;

public sealed class AppLogger : IAppLogger
{
    private readonly ILogger<AppLogger> _logger;
    private readonly object _fileLock = new();
    private readonly string _logDirectory;
    private readonly bool _fileEnabled;

    public AppLogger(ILogger<AppLogger> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _fileEnabled = !string.Equals(configuration["AppLogging:FileEnabled"], "false", StringComparison.OrdinalIgnoreCase);
        _logDirectory = ResolveLogDirectory(configuration["AppLogging:Directory"], environment.ContentRootPath);
    }

    public void Info(string category, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        using var scope = BeginScope(category, properties);
        _logger.LogInformation("{Message}", message);
        WriteFileLog("INFO", category, message, null, properties);
    }

    public void Warning(string category, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        using var scope = BeginScope(category, properties);
        _logger.LogWarning("{Message}", message);
        WriteFileLog("WARN", category, message, null, properties);
    }

    public void Error(string category, Exception exception, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        using var scope = BeginScope(category, properties);
        _logger.LogError(exception, "{Message}", message);
        WriteFileLog("ERROR", category, message, exception, properties);
    }

    private IDisposable? BeginScope(string category, IReadOnlyDictionary<string, object?>? properties)
    {
        var scope = new Dictionary<string, object?>
        {
            ["Category"] = category
        };

        if (properties is not null)
        {
            foreach (var property in properties)
            {
                scope[property.Key] = property.Value;
            }
        }

        return _logger.BeginScope(scope);
    }

    private void WriteFileLog(
        string level,
        string category,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?>? properties)
    {
        if (!_fileEnabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var logPath = Path.Combine(_logDirectory, $"netmind-{DateTimeOffset.Now:yyyyMMdd}.log");
            var line = FormatLogLine(level, category, message, exception, properties);
            lock (_fileLock)
            {
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "写入本地日志文件失败。");
        }
    }

    private static string FormatLogLine(
        string level,
        string category,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?>? properties)
    {
        var values = properties is null
            ? string.Empty
            : string.Join(" ", properties.Select(property => $"{property.Key}={property.Value}"));
        var exceptionText = exception is null ? string.Empty : $" Exception={exception.GetType().Name}: {exception.Message}";
        return $"{DateTimeOffset.Now:O} [{level}] [{category}] {message} {values}{exceptionText}".TrimEnd();
    }

    private static string ResolveLogDirectory(string? configuredDirectory, string contentRootPath)
    {
        var directory = string.IsNullOrWhiteSpace(configuredDirectory) ? "Logs" : configuredDirectory.Trim();
        return Path.IsPathRooted(directory)
            ? directory
            : Path.GetFullPath(Path.Combine(contentRootPath, directory));
    }
}
