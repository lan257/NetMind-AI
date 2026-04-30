namespace NetMind.Common.Logging;

public interface IAppLogger
{
    void Info(string category, string message, IReadOnlyDictionary<string, object?>? properties = null);

    void Warning(string category, string message, IReadOnlyDictionary<string, object?>? properties = null);

    void Error(string category, Exception exception, string message, IReadOnlyDictionary<string, object?>? properties = null);
}
