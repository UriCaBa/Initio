namespace Initio.Core.ViewModels;

public sealed partial class MainViewModel
{
    private async Task ExecuteBatchOperationAsync<TItem>(
        IReadOnlyList<TItem> targets,
        string batchDisplayName,
        string batchLabel,
        string actionVerb,
        string successVerb,
        string failureLabel,
        Func<TItem, string> getName,
        Func<TItem, string> getIdentifier,
        Action<TItem> markInProgress,
        Func<TItem, CancellationToken, Task<bool>> executeItemAsync,
        Action<TItem> onSuccess,
        Action<TItem> onFailure,
        Action? afterItem,
        CancellationToken cancellationToken)
    {
        var total = targets.Count;
        SetProgress(0, total);
        EtaText = "ETA: calculating...";
        ClearLogs();
        AppendLog($"Starting {batchLabel} of {total} item(s)...");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < total; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = targets[index];
                var position = index + 1;
                markInProgress(item);
                StatusText = $"[{position}/{total}] {actionVerb} {getName(item)}...";
                AppendLog($"[{position}/{total}] {actionVerb} {getName(item)} ({getIdentifier(item)})...");

                var succeededForItem = await executeItemAsync(item, cancellationToken).ConfigureAwait(false);
                if (succeededForItem)
                {
                    succeeded++;
                    onSuccess(item);
                    AppendLog($"  OK {getName(item)} {successVerb} successfully.");
                }
                else
                {
                    failed++;
                    onFailure(item);
                    AppendLog($"  FAIL {getName(item)} {failureLabel} failed.");
                }

                ProgressValue = position;
                UpdateEta(stopwatch, position, total);
                afterItem?.Invoke();
            }

            StatusText = $"Done - {succeeded} {successVerb}, {failed} failed.";
            AppendLog($"{batchDisplayName} complete: {succeeded} succeeded, {failed} failed ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (OperationCanceledException)
        {
            StatusText = $"{batchDisplayName} was cancelled.";
            AppendLog($"Cancelled after {succeeded} {successVerb} ({stopwatch.Elapsed:mm\\:ss}).");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AppendLog($"Unexpected error: {ex.Message}");
        }
    }
}
