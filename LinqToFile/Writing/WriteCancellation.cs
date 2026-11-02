namespace LinqToFile.Writing;

// Async input must be opened before knowing whether a range is empty. Its token therefore
// needs a forwarding gate: after the mutation boundary an already-created enumerator must
// no longer observe cancellation of the operation token.
internal sealed class WriteCancellation : IDisposable
{
    private readonly CancellationToken operation;
    private readonly CancellationTokenSource? forwarded;
    private CancellationTokenRegistration registration;
    private bool mutating;
    internal WriteCancellation(CancellationToken operation, bool inPlace)
    {
        this.operation = operation;
        if (inPlace && operation.CanBeCanceled)
        {
            forwarded = new();
            registration = operation.UnsafeRegister(static state => ((CancellationTokenSource)state!).Cancel(), forwarded);
        }
    }
    internal CancellationToken Token => mutating ? CancellationToken.None : forwarded?.Token ?? operation;
    internal void BeginMutation()
    {
        // Dispose waits for an in-flight forwarding callback before the final pre-write check.
        registration.Dispose();
        operation.ThrowIfCancellationRequested();
        mutating = true;
    }
    public void Dispose()
    {
        registration.Dispose();
        forwarded?.Dispose();
    }
}
