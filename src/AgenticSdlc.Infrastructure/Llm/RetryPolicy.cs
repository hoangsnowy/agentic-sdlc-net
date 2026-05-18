// AgenticSdlc.Infrastructure/Llm/RetryPolicy.cs
// Sprint 1 — Exponential backoff helper (3 retry, 1s/2s/4s) cho HTTP 429/5xx/timeout.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgenticSdlc.Infrastructure.Llm;

/// <summary>
/// Retry helper với exponential backoff. KHÔNG dùng Polly để giữ dependency tối thiểu trong Sprint 1
/// (Polly có thể được thêm sau qua <c>Microsoft.Extensions.Http.Resilience</c> nếu cần).
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Chạy <paramref name="operation"/> với retry. Retry chỉ trên:
    /// <list type="bullet">
    ///   <item><see cref="HttpRequestException"/></item>
    ///   <item><see cref="TaskCanceledException"/> không phải do <paramref name="cancellationToken"/></item>
    ///   <item><see cref="HttpResponseMessage"/> với status 429 hoặc 5xx (operation tự ném khi check)</item>
    /// </list>
    /// </summary>
    /// <typeparam name="T">Kiểu kết quả.</typeparam>
    /// <param name="operation">Hàm async thực hiện 1 lần gọi.</param>
    /// <param name="maxRetries">Số retry tối đa (không tính lần đầu).</param>
    /// <param name="baseDelay">Delay base (mặc định 1s). Lần thử thứ k delay = baseDelay * 2^(k-1).</param>
    /// <param name="logger">Logger (optional).</param>
    /// <param name="providerName">Tên provider cho log/exception.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả của lần gọi thành công.</returns>
    /// <exception cref="Domain.Llm.LlmException">Ném khi đã exhausted retry.</exception>
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries,
        TimeSpan? baseDelay = null,
        ILogger? logger = null,
        string providerName = "Llm",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be >= 0.");
        }

        var delay = baseDelay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User huỷ — không retry, rethrow luôn.
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastException = ex;

                if (attempt == maxRetries)
                {
                    break;
                }

                var wait = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, attempt));
                logger?.LogWarning(
                    "[{Provider}] Transient failure on attempt {Attempt}/{Max}: {Error}. Retrying in {Delay}ms.",
                    providerName, attempt + 1, maxRetries + 1, ex.Message, wait.TotalMilliseconds);

                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new Domain.Llm.LlmException(
            $"{providerName} request failed after {maxRetries + 1} attempts.",
            providerName,
            statusCode: lastException is TransientHttpException th ? th.StatusCode : null,
            innerException: lastException);
    }

    /// <summary>
    /// Trả về true nếu exception thuộc loại có thể retry.
    /// Bao gồm <see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/> (timeout),
    /// và custom <see cref="TransientHttpException"/>.
    /// </summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        TransientHttpException => true,
        HttpRequestException => true,
        TaskCanceledException => true, // HttpClient ném cái này khi timeout
        _ => false,
    };

    /// <summary>
    /// Trả về true nếu HTTP status đáng retry (429 hoặc 5xx).
    /// </summary>
    public static bool IsRetriableStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return code == 429 || (code >= 500 && code < 600);
    }
}

/// <summary>
/// Marker exception dùng nội bộ trong <see cref="RetryPolicy"/> để báo HTTP status đáng retry
/// (vd 429 hoặc 5xx). Caller (ClaudeClient, AzureOpenAiClient) ném exception này khi gặp
/// status code retry-able, để <see cref="RetryPolicy.ExecuteAsync"/> bắt và retry.
/// </summary>
internal sealed class TransientHttpException : Exception
{
    public int StatusCode { get; }

    public TransientHttpException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
