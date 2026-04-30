namespace NetMind.Common.Responses;

/// <summary>
/// Standard API response envelope.
/// </summary>
/// <typeparam name="TData">The response data type.</typeparam>
public sealed class ApiResult<TData>
{
    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the response message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the response data.
    /// </summary>
    public TData? Data { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <param name="message">The response message.</param>
    /// <returns>A successful API result.</returns>
    public static ApiResult<TData> Ok(TData data, string message = "成功")
    {
        return new ApiResult<TData>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <returns>A failed API result.</returns>
    public static ApiResult<TData> Fail(string message)
    {
        return new ApiResult<TData>
        {
            Success = false,
            Message = message,
            Data = default
        };
    }
}
