using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

public sealed record BoneNodeViewModel(Guid Id, string Name, string? Tag, float Length);
public sealed record BoneVisualViewModel(Guid Id, float X1, float Y1, float X2, float Y2, bool IsSelected);

public sealed class PartItemViewModel : INotifyPropertyChanged
{
    private bool isVisible = true;
    private int zOrder;
    private readonly Action<Guid, bool>? onVisibilityChanged;

    public Guid Id { get; }
    public string Name { get; }
    public string TexturePath { get; }
    public ImageSource? Image { get; }
    public float Width { get; }
    public float Height { get; }
    public float CenterX { get; }
    public float CenterY { get; }

    public int ZOrder
    {
        get => zOrder;
        set { zOrder = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value) return;
            isVisible = value;
            OnPropertyChanged();
            onVisibilityChanged?.Invoke(Id, value);
        }
    }

    public PartItemViewModel(Guid id, string name, string texturePath, ImageSource? image, float width, float height, float centerX, float centerY, int zOrder, bool isVisible = true, Action<Guid, bool>? onVisibilityChanged = null)
    {
        Id = id;
        Name = name;
        TexturePath = texturePath;
        Image = image;
        Width = width;
        Height = height;
        CenterX = centerX;
        CenterY = centerY;
        ZOrder = zOrder;
        this.isVisible = isVisible;
        this.onVisibilityChanged = onVisibilityChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record KeyframeViewModel(TimeSpan Time, double Frame, BezierEasing Easing);

public sealed class RigEditorViewModel : INotifyPropertyChanged
{
    private readonly RigEditorSession session;
    private readonly string? documentPath;
    private BoneNodeViewModel? selectedBone;
    private PartItemViewModel? selectedPart;
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

    private readonly Dictionary<string, ImageSource> imageCache = new(StringComparer.OrdinalIgnoreCase);

    public RigEditorViewModel(RigEditorSession session, string? documentPath = null)
    {
        this.session = session;
        this.documentPath = documentPath;
        session.Changed += OnRigChanged;

        ZoomInCommand = new RelayCommand(() => ZoomLevel *= 1.25);
        ZoomOutCommand = new RelayCommand(() => ZoomLevel /= 1.25);
        ResetZoomCommand = new RelayCommand(() => { ZoomLevel = 1.0; PanX = 0; PanY = 0; });

        UndoCommand = new RelayCommand(Undo, () => session.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => session.CanRedo);
        AddBoneCommand = new RelayCommand(AddSingleBone);
        AddChainCommand = new RelayCommand(AddChain);
        AddEightLegsCommand = new RelayCommand(AddEightLegs, () => SelectedBone is not null);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !string.IsNullOrWhiteSpace(this.documentPath));
        BackupCommand = new AsyncRelayCommand(BackupAsync, () => !string.IsNullOrWhiteSpace(this.documentPath));

        MovePartUpCommand = new RelayCommand(MovePartUp, () => SelectedPart is not null);
        MovePartDownCommand = new RelayCommand(MovePartDown, () => SelectedPart is not null);
        TogglePartVisibilityCommand = new RelayCommand(TogglePartVisibility, () => SelectedPart is not null);

        RefreshMotions();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BoneNodeViewModel> Bones { get; } = [];
    public ObservableCollection<BoneVisualViewModel> BoneVisuals { get; } = [];
    public ObservableCollection<PartItemViewModel> Parts { get; } = [];
    public ObservableCollection<string> Motions { get; } = [];
    public ObservableCollection<KeyframeViewModel> Keyframes { get; } = [];

    private double zoomLevel = 1.0;
    private double panX = 0;
    private double panY = 0;

    public double ZoomLevel
    {
        get => zoomLevel;
        set
        {
            zoomLevel = Math.Clamp(value, 0.1, 10.0);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomPercentage));
        }
    }

    public double PanX
    {
        get => panX;
        set { panX = value; OnPropertyChanged(); }
    }

    public double PanY
    {
        get => panY;
        set { panY = value; OnPropertyChanged(); }
    }

    public string ZoomPercentage => $"{Math.Round(ZoomLevel * 100)}%";

    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }

    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand AddBoneCommand { get; }
    public ICommand AddChainCommand { get; }
    public ICommand AddEightLegsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand BackupCommand { get; }

    public ICommand MovePartUpCommand { get; }
    public ICommand MovePartDownCommand { get; }
    public ICommand TogglePartVisibilityCommand { get; }

    public string RigName => session.Rig.Name;
    public bool IsDirty => session.IsDirty;

    private int newChainCount = 4;
    private float newChainLength = 40f;
    private readonly Dictionary<Guid, bool> visibilityStateMap = [];

    public int NewChainCount
    {
        get => newChainCount;
        set { newChainCount = Math.Clamp(value, 1, 30); OnPropertyChanged(); }
    }

    public float NewChainLength
    {
        get => newChainLength;
        set { newChainLength = Math.Clamp(value, 5f, 500f); OnPropertyChanged(); }
    }

    public float SelectedBoneLength
    {
        get => SelectedBone?.Length ?? 0f;
        set
        {
            if (SelectedBone is null || MathF.Abs(SelectedBone.Length - value) < 0.01f) return;
            var boneId = SelectedBone.Id;
            var newLength = MathF.Max(1f, value);
            session.Apply("Change bone length", rig => RigOperations.UpdateBone(rig, boneId, b => b with { Length = newLength }));
        }
    }

    public BoneNodeViewModel? SelectedBone
    {
        get => selectedBone;
        set
        {
            if (Equals(selectedBone, value)) return;
            selectedBone = value;
            session.SelectedBoneId = value?.Id;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBoneLength));
            RefreshVisuals();
            RefreshKeyframes();
            NotifyCommands();
        }
    }

    public PartItemViewModel? SelectedPart
    {
        get => selectedPart;
        set
        {
            if (Equals(selectedPart, value)) return;
            selectedPart = value;
            OnPropertyChanged();
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

    public void ImportDirectory(string directory) => ImportFileOrDirectory(directory);

    public void ImportFileOrDirectory(string path)
    {
        var imported = CutoutFolderImporter.Import(path, session.Rig.Name);
        session.Apply("Import cut-out images or PSD", _ => imported);
        Status = $"Imported {imported.Parts.Count} parts";
    }

    public void ApplyPointerDelta(Vector2 delta)
    {
        if (SelectedBone is null || delta.LengthSquared() < 0.0001f) return;
        var scaledDelta = delta / (float)Math.Max(0.01, ZoomLevel);
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
                    Translation = SnapEngine.SnapPoint(bone.Translation + scaledDelta, settings).Value,
                },
                EditorTransformTool.Rotate => bone with
                {
                    Rotation = SnapEngine.SnapAngle(bone.Rotation + scaledDelta.X * 0.01f, settings),
                },
                EditorTransformTool.Scale => bone with
                {
                    Scale = Vector2.Max(new Vector2(0.01f), bone.Scale + new Vector2(scaledDelta.X, -scaledDelta.Y) * 0.01f),
                },
                EditorTransformTool.Ik => bone with
                {
                    Translation = SnapEngine.SnapPoint(bone.Translation + scaledDelta, settings).Value,
                },
                _ => bone,
            };
        }));
    }

    private void AddSingleBone()
    {
        var parentId = SelectedBone?.Id ?? session.Rig.Bones.FirstOrDefault()?.Id;
        var parent = session.Rig.Bones.FirstOrDefault(x => x.Id == parentId);
        var count = session.Rig.Bones.Count;
        var newBone = new BoneDefinition
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = $"Bone_{count}",
            RetargetTag = $"Bone_{count}",
            Translation = parent is null ? Vector2.Zero : new Vector2(parent.Length, 0),
            Length = 50,
        };
        session.Apply("Add single bone", rig => RigOperations.AddBones(rig, [newBone]));
        SelectedBone = Bones.FirstOrDefault(x => x.Id == newBone.Id);
    }

    private void AddChain()
    {
        var parent = SelectedBone?.Id;
        var origin = parent is { } id
            ? new Vector2(session.Rig.Bones.Single(x => x.Id == id).Length, 0)
            : Vector2.Zero;
        var count = NewChainCount;
        var len = NewChainLength;
        var bones = BoneArrayGenerator.CreateChain(count, len, "Chain", origin, parentId: parent, tagPrefix: "Chain");
        session.Apply($"Add {count}-bone chain", rig => RigOperations.AddBones(rig, bones));
    }

    private void AddEightLegs()
    {
        var bones = BoneArrayGenerator.CreateRadialLimbs(SelectedBone!.Id, 8, 3, 35, 20, "Leg");
        session.Apply("Add eight radial legs", rig => RigOperations.AddBones(rig, bones));
    }

    private void MovePartUp()
    {
        if (SelectedPart is null) return;
        var targetPartId = SelectedPart.Id;
        session.Apply("Move part forward", rig =>
        {
            var part = rig.Parts.FirstOrDefault(x => x.Id == targetPartId);
            if (part is null) return rig;
            return rig with
            {
                Parts = rig.Parts.Select(p => p.Id == targetPartId ? p with { ZOrder = p.ZOrder + 1 } : p).ToArray()
            };
        });
    }

    private void MovePartDown()
    {
        if (SelectedPart is null) return;
        var targetPartId = SelectedPart.Id;
        session.Apply("Move part backward", rig =>
        {
            var part = rig.Parts.FirstOrDefault(x => x.Id == targetPartId);
            if (part is null) return rig;
            return rig with
            {
                Parts = rig.Parts.Select(p => p.Id == targetPartId ? p with { ZOrder = p.ZOrder - 1 } : p).ToArray()
            };
        });
    }

    private void TogglePartVisibility()
    {
        if (SelectedPart is null) return;
        SelectedPart.IsVisible = !SelectedPart.IsVisible;
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
        var selectedBoneId = SelectedBone?.Id;
        var selectedPartId = SelectedPart?.Id;

        Bones.Clear();
        foreach (var bone in session.Rig.Bones)
            Bones.Add(new BoneNodeViewModel(bone.Id, bone.Name, bone.RetargetTag, bone.Length));
        selectedBone = Bones.FirstOrDefault(x => x.Id == selectedBoneId);

        RefreshParts();
        selectedPart = Parts.FirstOrDefault(x => x.Id == selectedPartId);

        OnPropertyChanged(nameof(SelectedBone));
        OnPropertyChanged(nameof(SelectedBoneLength));
        OnPropertyChanged(nameof(SelectedPart));
        OnPropertyChanged(nameof(RigName));
        OnPropertyChanged(nameof(IsDirty));
        RefreshVisuals();
        NotifyCommands();
    }

    private void RefreshParts()
    {
        Parts.Clear();
        var docDir = documentPath is null ? null : Path.GetDirectoryName(documentPath);
        foreach (var part in session.Rig.Parts.OrderBy(x => x.ZOrder))
        {
            ImageSource? img = null;
            if (!string.IsNullOrWhiteSpace(docDir) && !string.IsNullOrWhiteSpace(part.TexturePath))
            {
                var fullPath = Path.IsPathRooted(part.TexturePath) ? part.TexturePath : Path.Combine(docDir, part.TexturePath);
                if (File.Exists(fullPath))
                {
                    img = LoadImage(fullPath);
                }
            }

            var minX = part.Vertices.Select(v => v.Position.X).Min();
            var maxX = part.Vertices.Select(v => v.Position.X).Max();
            var minY = part.Vertices.Select(v => v.Position.Y).Min();
            var maxY = part.Vertices.Select(v => v.Position.Y).Max();
            var w = MathF.Max(1, maxX - minX);
            var h = MathF.Max(1, maxY - minY);

            // Preserving exact PSD offset relative to Canvas center (450, 300)
            var canvasLeft = 450f + minX;
            var canvasTop = 300f - maxY;

            var isVisible = visibilityStateMap.TryGetValue(part.Id, out var vis) ? vis : true;
            visibilityStateMap[part.Id] = isVisible;

            Parts.Add(new PartItemViewModel(part.Id, part.Name, part.TexturePath, img, w, h, canvasLeft, canvasTop, part.ZOrder, isVisible, OnPartVisibilityChanged));
        }
    }

    private void OnPartVisibilityChanged(Guid partId, bool isVisible)
    {
        visibilityStateMap[partId] = isVisible;
    }

    private ImageSource? LoadImage(string path)
    {
        if (imageCache.TryGetValue(path, out var cached)) return cached;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            imageCache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
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
        ((RelayCommand)MovePartUpCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MovePartDownCommand).RaiseCanExecuteChanged();
        ((RelayCommand)TogglePartVisibilityCommand).RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
