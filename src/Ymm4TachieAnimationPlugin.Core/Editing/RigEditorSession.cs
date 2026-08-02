using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Serialization;

namespace Ymm4TachieAnimationPlugin.Core.Editing;

public sealed class RigChangedEventArgs(RigDefinition rig, string description) : EventArgs
{
    public RigDefinition Rig { get; } = rig;
    public string Description { get; } = description;
}

public sealed class RigEditorSession
{
    private readonly UndoRedoHistory<RigDefinition> history;
    private RigDefinition savedState;

    public RigEditorSession(RigDefinition rig, int? historyCapacity = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        rig.Validate();
        history = new UndoRedoHistory<RigDefinition>(rig, historyCapacity);
        savedState = rig;
    }

    public event EventHandler<RigChangedEventArgs>? Changed;

    public RigDefinition Rig => history.Current;
    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;
    public bool IsDirty => !ReferenceEquals(Rig, savedState);
    public int HistoryCount => history.Count;
    public Guid? SelectedBoneId { get; set; }
    public Guid? SelectedPartId { get; set; }

    public void Apply(string description, Func<RigDefinition, RigDefinition> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var updated = operation(Rig);
        if (ReferenceEquals(updated, Rig)) return;
        updated.Validate();
        history.Push(description, updated);
        Changed?.Invoke(this, new RigChangedEventArgs(updated, description));
    }

    public void Undo()
    {
        var rig = history.Undo();
        Changed?.Invoke(this, new RigChangedEventArgs(rig, "Undo"));
    }

    public void Redo()
    {
        var rig = history.Redo();
        Changed?.Invoke(this, new RigChangedEventArgs(rig, "Redo"));
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        await WriteAtomicallyAsync(path, RigSerializer.SerializeRig(Rig), cancellationToken).ConfigureAwait(false);
        savedState = Rig;
    }

    public Task BackupAsync(string backupDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        var safeName = string.Concat(Rig.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var file = $"{safeName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.rig.backup.json";
        return WriteAtomicallyAsync(Path.Combine(backupDirectory, file), RigSerializer.SerializeRig(Rig), cancellationToken);
    }

    private static async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("The path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
