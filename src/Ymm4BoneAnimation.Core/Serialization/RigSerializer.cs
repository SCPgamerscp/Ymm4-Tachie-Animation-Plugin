using System.Text.Json;
using System.Text.Json.Serialization;
using Ymm4BoneAnimation.Core.Animation;
using Ymm4BoneAnimation.Core.Model;

namespace Ymm4BoneAnimation.Core.Serialization;

public static class RigSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string SerializeRig(RigDefinition rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        rig.Validate();
        return JsonSerializer.Serialize(rig, Options);
    }

    public static RigDefinition DeserializeRig(string json)
    {
        var rig = JsonSerializer.Deserialize<RigDefinition>(json, Options)
            ?? throw new JsonException("The rig document is empty.");
        rig.Validate();
        return rig;
    }

    public static string SerializeAnimation(AnimationClip animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        return JsonSerializer.Serialize(animation, Options);
    }

    public static AnimationClip DeserializeAnimation(string json) =>
        JsonSerializer.Deserialize<AnimationClip>(json, Options)
        ?? throw new JsonException("The animation document is empty.");
}
