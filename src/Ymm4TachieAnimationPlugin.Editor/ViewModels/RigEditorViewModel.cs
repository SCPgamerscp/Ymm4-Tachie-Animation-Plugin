using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Ymm4TachieAnimationPlugin.Core.Animation;
using Ymm4TachieAnimationPlugin.Core.Editing;
using Ymm4TachieAnimationPlugin.Core.Model;
using Ymm4TachieAnimationPlugin.Core.Rigging;
using Ymm4TachieAnimationPlugin.Core.Runtime;
using Ymm4TachieAnimationPlugin.Core.Serialization;
using Ymm4TachieAnimationPlugin.Editor.Commands;
using Ymm4TachieAnimationPlugin.Editor.Importing;

namespace Ymm4TachieAnimationPlugin.Editor.ViewModels;

public enum EditorBackgroundMode
{
    Checkerboard,
    Solid,
    Ymm4Live,
}

public enum EditorTransformTool
{
    Translate,
    Rotate,
    Scale,
    Ik,
}

public sealed record BoneNodeViewModel(Guid Id, string Name, string? Tag);
public sealed record BoneVisualViewModel(Guid Id, float X1, float Y1, float X2, float Y2, bool IsSelected);
public sealed record KeyframeViewModel(TimeSpan Time, double Frame, BezierEasing Easing);

public sealed class RigEditorViewModel : INotifyPropertyChanged
{
    private readonly RigEditorSession session;
    private readonly string? documentPath;
    private BoneNodeViewModel? selectedBone;
    private string status = "Ready";
    private EditorBackgroundMode backgroundMode;
    private EditorTransformTool activeTool;
    private bool gridSnapEnabled = true;
    private bool angleSnapEnabled = true;
    private bool guideSnapEnabled = true;
    private string? selectedMotion;
    private AnimationClip? selectedClip;
    private KeyframeViewModel? selectedKeyframe;
    private double currentFrame;
    private bool autoKeyEnabled = true;

    public RigEditorViewModel(RigEditorSession session, string? documentPath = null)
    {
        this.session = session;
        this.documentPath = documentPath;
        session.Changed += OnRigChanged;
        UndoCommand = new RelayCommand(Undo, () => session.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => session.CanRedo);
        AddChainCommand = new RelayCommand(AddChain);
        AddEightLegsCommand = new RelayCommand(AddEightLegs, () => SelectedBone is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !string.IsNullOrWhiteSpace(this.documentPath));
        BackupCommand = new AsyncRelayCommand(BackupAsync, () => !string.IsNullOrWhiteSpace(this.documentPath));
        RefreshMotions();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BoneNodeViewModel> Bones { get; } = [];
    public ObservableCollection<BoneVisualViewModel> BoneVisuals { get; } = [];
    public ObservableCollection<string> Motions { get; } = [];
    public ObservableCollection<KeyframeViewModel> Keyframes { get; } = [];

    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand AddChainCommand { get; }
    public ICommand AddEightLegsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand BackupCommand { get; }

    public string RigName => session.Rig.Name;
    public bool IsDirty => session.IsDirty;

    public BoneNodeViewModel? SelectedBone
    {
        get => selectedBone;
        set
        {
            if (Equals(selectedBone, value)) return;
            selectedBone = value;
            session.SelectedBoneId = value?.Id;
            OnPropertyChanged();
            RefreshVisuals();
            RefreshKeyframes();
            NotifyCommands();
        }
    }

    public string Status
    {
        get => status;
        private set { status = value; OnPropertyChanged(); }
    }

    public EditorBackgroundMode BackgroundMode
    {
        get => backgroundMode;
        set { backgroundMode = value; OnPropertyChanged(); }
    }

    public EditorTransformTool ActiveTool
    {
        get => activeTool;
        set { activeTool = value; OnPropertyChanged(); Status = $"Tool: {value}"; }
    }

    public bool GridSnapEnabled
    {
        get => gridSnapEnabled;
        set { gridSnapEnabled = value; OnPropertyChanged(); }
    }

    public bool AngleSnapEnabled
    {
        get => angleSnapEnabled;
        set { angleSnapEnabled = value; OnPropertyChanged(); }
    }

    public bool GuideSnapEnabled
    {
        get => guideSnapEnabled;
        set { guideSnapEnabled = value; OnPropertyChanged(); }
    }

    public string? SelectedMotion
    {
        get => selectedMotion;
        set
        {
            if (selectedMotion == value) return;
            selectedMotion = value;
            OnPropertyChanged();
            LoadSelectedMotion();
            Status = value is null ? "No motion selected" : $"Motion: {value}";
        }
    }

    public KeyframeViewModel? SelectedKeyframe
    {
        get => selectedKeyframe;
        set { selectedKeyframe = value; OnPropertyChanged(); }
    }

    public double CurrentFrame
    {
        get => currentFrame;
        set { currentFrame = Math.Max(0, value); OnPropertyChanged(); }
    }

    public bool AutoKeyEnabled
    {
        get => autoKeyEnabled;
        set { autoKeyEnabled = value; OnPropertyChanged(); }
    }

    public void AddKeyframe()
    {
        if (SelectedBone is null || string.IsNullOrWhiteSpace(SelectedMotion)) return;
        selectedClip ??= new AnimationClip { Name = SelectedMotion, Duration = TimeSpan.FromSeconds(2) };
        var bone = session.Rig.Bones.Single(x => x.Id == SelectedBone.Id);
        var value = new BoneTransform(bone.Translation, bone.Rotation, bone.Scale, bone.ZOrder);
        selectedClip = AnimationTimelineEditor.SetKeyframe(
            selectedClip,
            bone.Id,
            TimeSpan.FromSeconds(CurrentFrame / 30),
            value,
            autoKeyEnabled: AutoKeyEnabled);
        SaveSelectedMotion();
    }

    public void DeleteKeyframe()
    {
        if (SelectedBone is null || selectedClip is null || SelectedKeyframe is null) return;
        selectedClip = AnimationTimelineEditor.DeleteKeyframe(selectedClip, SelectedBone.Id, SelectedKeyframe.Time);
        SaveSelectedMotion();
    }

    public void ImportDirectory(string directory)
    {
        ImportFileOrDirectory(directory);
    }

    public void ImportFileOrDirectory(string path)
    {
        var imported = CutoutFolderImporter.Import(path, session.Rig.Name);
        session.Apply("Import cut-out images or PSD", _ => imported);
        Status = $"Imported {imported.Parts.Count} parts";
    }

    public void ApplyPointerDelta(Vector2 delta)
    {
        if (SelectedBone is null || delta.LengthSquared() < 0.0001f) return;
        var id = SelectedBone.Id;
        session.Apply($"{ActiveTool} bone", rig => RigOperations.UpdateBone(rig, id, bone =>
        {
            var settings = new SnapSettings
            {
                GridEnabled = GridSnapEnabled,
                AngleEnabled = AngleSnapEnabled,
                GuidesEnabled = GuideSnapEnabled,
            };
            return ActiveTool switch
            {
                EditorTransformTool.Translate => bone with
                {
                    Translation = SnapEngine.SnapPoint(bone.Translation + delta, settings).Value,
                },
                EditorTransformTool.Rotate => bone with
                {
                    Rotation = SnapEngine.SnapAngle(bone.Rotation + delta.X * 0.01f, settings),
                },
                EditorTransformTool.Scale => bone with
                {
                    Scale = Vector2.Max(new Vector2(0.01f), bone.Scale + new Vector2(delta.X, -delta.Y) * 0.01f),
                },
                EditorTransformTool.Ik => bone with
                {
                    Translation = SnapEngine.SnapPoint(bone.Translation + delta, settings).Value,
                },
                _ => bone,
            };
        }));
    }

    private void AddChain()
    {
        var parent = SelectedBone?.Id;
        var origin = parent is { } id
            ? new Vector2(session.Rig.Bones.Single(x => x.Id == id).Length, 0)
            : Vector2.Zero;
        var bones = BoneArrayGenerator.CreateChain(4, 40, "Chain", origin, parentId: parent, tagPrefix: "Chain");
        session.Apply("Add four-bone chain", rig => RigOperations.AddBones(rig, bones));
    }

    private void AddEightLegs()
    {
        var bones = BoneArrayGenerator.CreateRadialLimbs(SelectedBone!.Id, 8, 3, 35, 20, "Leg");
        session.Apply("Add eight radial legs", rig => RigOperations.AddBones(rig, bones));
    }

    private void Undo() => session.Undo();
    private void Redo() => session.Redo();

    private async Task SaveAsync()
    {
        try
        {
            await session.SaveAsync(documentPath!);
            Status = $"Saved {Path.GetFileName(documentPath)}";
            OnPropertyChanged(nameof(IsDirty));
        }
        catch (Exception exception)
        {
            Status = $"Save failed: {exception.Message}";
        }
    }

    private async Task BackupAsync()
    {
        try
        {
            var backupDirectory = Path.Combine(Path.GetDirectoryName(documentPath!)!, ".backups");
            await session.BackupAsync(backupDirectory);
            Status = "Backup created";
        }
        catch (Exception exception)
        {
            Status = $"Backup failed: {exception.Message}";
        }
    }

    private void OnRigChanged(object? sender, RigChangedEventArgs args)
    {
        Status = args.Description;
        Refresh();
    }

    private void LoadSelectedMotion()
    {
        selectedClip = null;
        var directory = documentPath is null ? null : Path.GetDirectoryName(documentPath);
        if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(SelectedMotion))
        {
            var path = Path.Combine(directory, $"{SelectedMotion}.ymm4anim");
            if (File.Exists(path)) selectedClip = RigSerializer.DeserializeAnimation(File.ReadAllText(path));
        }
        RefreshKeyframes();
    }

    private void SaveSelectedMotion()
    {
        var directory = documentPath is null ? null : Path.GetDirectoryName(documentPath);
        if (selectedClip is null || string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(SelectedMotion)) return;
        File.WriteAllText(Path.Combine(directory, $"{SelectedMotion}.ymm4anim"), RigSerializer.SerializeAnimation(selectedClip));
        RefreshKeyframes();
        Status = "Motion saved";
    }

    private void RefreshKeyframes()
    {
        Keyframes.Clear();
        if (SelectedBone is null || selectedClip is null) return;
        var track = selectedClip.Tracks.FirstOrDefault(x => x.BoneId == SelectedBone.Id);
        if (track is null) return;
        foreach (var key in track.Keyframes)
            Keyframes.Add(new KeyframeViewModel(key.Time, key.Time.TotalSeconds * 30, key.Easing));
    }

    private void RefreshMotions()
    {
        Motions.Clear();
        var directory = documentPath is null ? null : Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.ymm4anim").OrderBy(Path.GetFileName))
            Motions.Add(Path.GetFileNameWithoutExtension(path));
        SelectedMotion = Motions.FirstOrDefault();
        if (SelectedMotion is null) RefreshKeyframes();
    }

    private void Refresh()
    {
        var selectedId = SelectedBone?.Id;
        Bones.Clear();
        foreach (var bone in session.Rig.Bones)
            Bones.Add(new BoneNodeViewModel(bone.Id, bone.Name, bone.RetargetTag));
        selectedBone = Bones.FirstOrDefault(x => x.Id == selectedId);
        OnPropertyChanged(nameof(SelectedBone));
        OnPropertyChanged(nameof(RigName));
        OnPropertyChanged(nameof(IsDirty));
        RefreshVisuals();
        NotifyCommands();
    }

    private void RefreshVisuals()
    {
        BoneVisuals.Clear();
        if (session.Rig.Bones.Count == 0) return;
        var globals = new RigEvaluator(session.Rig).EvaluateGlobals(Pose.FromRestPose(session.Rig));
        foreach (var bone in session.Rig.Bones)
        {
            var start = globals[bone.Id].Translation;
            var end = Vector2.Transform(new Vector2(bone.Length, 0), globals[bone.Id]);
            BoneVisuals.Add(new BoneVisualViewModel(
                bone.Id, start.X, start.Y, end.X, end.Y, bone.Id == SelectedBone?.Id));
        }
    }

    private void NotifyCommands()
    {
        ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddEightLegsCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)BackupCommand).RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
