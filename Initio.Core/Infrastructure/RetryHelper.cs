namespace Initio.Core.Infrastructure;

public static class RetryHelper
{
    public static async Task<bool> ExecuteAsync(
        int maxAttempts,
        Func<int, CancellationToken, Task<bool>> operation,
        Action<int>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attempt > 1)
            {
                onRetry?.Invoke(attempt);
            }

            if (await operation(attempt, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}
