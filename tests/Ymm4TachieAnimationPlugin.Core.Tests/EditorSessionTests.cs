using Ymm4TachieAnimationPlugin.Core.Editing;
using Ymm4TachieAnimationPlugin.Core.Serialization;

namespace Ymm4TachieAnimationPlugin.Core.Tests;

public sealed class EditorSessionTests
{
    [Fact]
    public void UndoRedo_RestoresStatesAndDirtyMarker()
    {
        var rig = RigFixtures.TwoBone(out var root, out _);
        var session = new RigEditorSession(rig);
        session.Apply("Rename root", current => RigOperations.UpdateBone(current, root, bone => bone with { Name = "renamed" }));
        Assert.True(session.IsDirty);
        Assert.Equal("renamed", session.Rig.Bones[0].Name);

        session.Undo();
        Assert.False(session.IsDirty);
        Assert.Equal("root", session.Rig.Bones[0].Name);
        session.Redo();
        Assert.Equal("renamed", session.Rig.Bones[0].Name);
    }

    [Fact]
    public async Task SaveAndBackup_WriteValidRigDocuments()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "session-output", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "rig.json");
        try
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var session = new RigEditorSession(RigFixtures.TwoBone(out _, out _));
            await session.SaveAsync(path, cancellationToken);
            await session.BackupAsync(Path.Combine(directory, "backups"), cancellationToken);
            Assert.False(session.IsDirty);
            Assert.Equal("test", RigSerializer.DeserializeRig(await File.ReadAllTextAsync(path, cancellationToken)).Name);
            Assert.Single(Directory.GetFiles(Path.Combine(directory, "backups"), "*.backup.json"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void NewOperationAfterUndo_DiscardsRedoBranch()
    {
        var history = new UndoRedoHistory<int>(0);
        history.Push("one", 1);
        history.Push("two", 2);
        history.Undo();
        history.Push("replacement", 3);
        Assert.False(history.CanRedo);
        Assert.Equal(3, history.Current);
    }
}
