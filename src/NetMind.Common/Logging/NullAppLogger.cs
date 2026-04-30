namespace NetMind.Common.Logging;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Info(string category, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
    }

    public void Warning(string category, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
    }

    public void Error(string category, Exception exception, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
    }
}
