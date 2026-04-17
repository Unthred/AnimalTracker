namespace AnimalTracker.State;

public sealed class UiProgressState
{
    private int activeOperations;
    public event Action? Changed;

    public bool IsActive => activeOperations > 0;
    public string? CurrentLabel { get; private set; }

    public IDisposable Begin(string? label = null)
    {
        activeOperations++;
        if (!string.IsNullOrWhiteSpace(label))
            CurrentLabel = label.Trim();
        Changed?.Invoke();
        return new Scope(this);
    }

    private void End()
    {
        if (activeOperations > 0)
            activeOperations--;
        if (activeOperations == 0)
            CurrentLabel = null;
        Changed?.Invoke();
    }

    private sealed class Scope(UiProgressState state) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            state.End();
        }
    }
}
