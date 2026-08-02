namespace Ymm4BoneAnimation.Core.Editing;

public sealed record HistoryEntry<T>(string Description, T State, DateTimeOffset CreatedAt);

public sealed class UndoRedoHistory<T>
{
    private readonly List<HistoryEntry<T>> entries;
    private readonly int? capacity;
    private int position;

    public UndoRedoHistory(T initialState, int? capacity = null)
    {
        if (capacity is <= 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
        entries = [new HistoryEntry<T>("Initial state", initialState, DateTimeOffset.UtcNow)];
    }

    public T Current => entries[position].State;
    public string CurrentDescription => entries[position].Description;
    public bool CanUndo => position > 0;
    public bool CanRedo => position < entries.Count - 1;
    public int Count => entries.Count;

    public void Push(string description, T state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (CanRedo) entries.RemoveRange(position + 1, entries.Count - position - 1);
        entries.Add(new HistoryEntry<T>(description, state, DateTimeOffset.UtcNow));
        position = entries.Count - 1;
        TrimToCapacity();
    }

    public T Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("There is no operation to undo.");
        position--;
        return Current;
    }

    public T Redo()
    {
        if (!CanRedo) throw new InvalidOperationException("There is no operation to redo.");
        position++;
        return Current;
    }

    private void TrimToCapacity()
    {
        if (capacity is not { } maximum || entries.Count <= maximum) return;
        var removeCount = entries.Count - maximum;
        entries.RemoveRange(0, removeCount);
        position -= removeCount;
    }
}
