using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Animation;

public sealed class Retargeter
{
    public AnimationClip Retarget(AnimationClip sourceClip, RigDefinition sourceRig, RigDefinition targetRig)
    {
        sourceRig.Validate();
        targetRig.Validate();
        var sourceBones = sourceRig.Bones.ToDictionary(x => x.Id);
        var targetsByTag = targetRig.Bones
            .Where(x => !string.IsNullOrWhiteSpace(x.RetargetTag))
            .GroupBy(x => x.RetargetTag!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var tracks = new List<BoneTrack>();
        foreach (var track in sourceClip.Tracks)
        {
            if (!sourceBones.TryGetValue(track.BoneId, out var sourceBone) ||
                string.IsNullOrWhiteSpace(sourceBone.RetargetTag) ||
                !targetsByTag.TryGetValue(sourceBone.RetargetTag, out var targetBone))
                continue;

            var scale = sourceBone.Length > 1e-5f ? targetBone.Length / sourceBone.Length : 1f;
            tracks.Add(new BoneTrack
            {
                BoneId = targetBone.Id,
                Keyframes = track.Keyframes.Select(key => key with
                {
                    Value = key.Value with { Translation = key.Value.Translation * scale },
                }).ToArray(),
            });
        }

        return sourceClip with { Name = $"{sourceClip.Name} ({targetRig.Name})", Tracks = tracks };
    }
}
