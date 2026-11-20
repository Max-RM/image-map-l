using fNbt;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TryashtarUtils.Nbt;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ImageMap4;

public class VersionManager
{
    private static readonly List<(JavaVersionCheck check, IJavaVersion version)> JavaVersions = new();
    private static readonly List<(BedrockVersionCheck check, IBedrockVersion version)> BedrockVersions = new();
    static VersionManager()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlColorConverter())
            .WithTypeConverter(new YamlNbtConverter())
            .WithTypeConverter(new YamlNbtPathConverter())
            .Build();
        var updates = deserializer.Deserialize<UpdateLists>(ImageMap4.CMD.Properties.Resources.versions);
        var java = new JavaVersionBuilder();
        foreach (var update in updates.Java)
        {
            java.Add(update, update.Check.DataVersion);
            JavaVersions.Add((update.Check, java.Build()));
        }
        var bedrock = new BedrockVersionBuilder();
        foreach (var update in updates.Bedrock)
        {
            bedrock.Add(update);
            BedrockVersions.Add((update.Check, bedrock.Build()));
        }
        VerifyBedrockVersionChecks();
    }

    public static IJavaVersion? DetermineJavaVersion(NbtCompound leveldat)
    {
        return JavaVersions.LastOrDefault(x => x.check.Passes(leveldat)).version;
    }

    public static IBedrockVersion? DetermineBedrockVersion(NbtCompound leveldat)
    {
        var matched = BedrockVersions.LastOrDefault(x => x.check.Passes(leveldat)).version;
        if (matched != null)
            return matched;
        // Older than the first YAML entry (PE 1.0) or missing version tags: numeric item IDs.
        return BedrockVersions.Count > 0 ? BedrockVersions[0].version : null;
    }

    public static string FormatBedrockOpenedVersion(NbtCompound leveldat)
    {
        var list = leveldat.Get<NbtList>("lastOpenedWithVersion");
        if (list != null && list.Count > 0)
            return JoinVersionParts(list);
        var parts = BedrockVersionCheck.ReadWorldVersion(leveldat);
        if (parts == null || parts.Length == 0)
            return "";
        return string.Join(".", parts);
    }

    public static string FormatJavaOpenedVersion(NbtCompound? data)
    {
        var name = data?.Get<NbtCompound>("Version")?.Get<NbtString>("Name")?.Value;
        return string.IsNullOrWhiteSpace(name) ? "" : name;
    }

    private static string JoinVersionParts(NbtList list)
    {
        var parts = new string[list.Count];
        for (int i = 0; i < list.Count; i++)
            parts[i] = list[i].IntValue.ToString();
        return string.Join(".", parts);
    }

    private static void VerifyBedrockVersionChecks()
    {
        static NbtCompound Level(params int[] parts)
        {
            var list = new NbtList("lastOpenedWithVersion");
            foreach (var n in parts)
                list.Add(new NbtInt(n));
            return new NbtCompound { list };
        }
        static string? NameOf(params int[] parts) => (DetermineBedrockVersion(Level(parts)) as BedrockVersion)?.Name;
        if (NameOf(1, 0) != "1.0")
            throw new InvalidOperationException("Bedrock 1.0 worlds must use numeric item IDs.");
        if (NameOf(1, 6) != "1.6")
            throw new InvalidOperationException("Bedrock 1.6 must match itself (string item IDs).");
        if (DetermineBedrockVersion(Level(1, 13))?.SupportsStructures != true)
            throw new InvalidOperationException("Bedrock 1.13 must support structuretemplate_ keys.");
        if (DetermineBedrockVersion(Level(1, 12))?.SupportsStructures == true)
            throw new InvalidOperationException("Bedrock 1.12 must not write structure templates.");
        if (NameOf(1, 16) != "1.13")
            throw new InvalidOperationException("Bedrock 1.16.0 is older than filled_map (1.16.100).");
        if ((DetermineBedrockVersion(new NbtCompound()) as BedrockVersion)?.Name != "1.0")
            throw new InvalidOperationException("Worlds without lastOpenedWithVersion must fall back to 1.0.");
    }
}

internal class UpdateLists
{
    public List<JavaUpdate> Java;
    public List<BedrockUpdate> Bedrock;
}

internal class YamlColorConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(Rgba32);
    }

    public object? ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();
        Color.TryParse(scalar.Value, out var color);
        return color;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        throw new NotSupportedException();
    }
}

internal class YamlNbtConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return typeof(NbtTag).IsAssignableFrom(type);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        if (parser.TryConsume<MappingStart>(out _))
        {
            var compound = new NbtCompound();
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                var value = (NbtTag)ReadYaml(parser, typeof(NbtTag));
                value.Name = key;
                compound.Add(value);
            }
            return compound;
        }
        if (parser.TryConsume<SequenceStart>(out _))
        {
            var list = new NbtList();
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                var value = (NbtTag)ReadYaml(parser, typeof(NbtTag));
                list.Add(value);
            }
            return list;
        }
        else
        {
            var scalar = parser.Consume<Scalar>();
            if (SnbtParser.ClassicTryParse(scalar.Value, false, out var result))
                return result;
            return new NbtString(scalar.Value);
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        throw new NotSupportedException();
    }
}

internal class YamlNbtPathConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return typeof(NbtPath).IsAssignableFrom(type);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        if (parser.TryConsume<SequenceStart>(out _))
        {
            var list = new List<NbtPathNode>();
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                var value = parser.Consume<Scalar>();
                list.Add(NbtPathNode.Parse(value.Value));
            }
            return new NbtPath("", list.ToArray());
        }
        else
        {
            var scalar = parser.Consume<Scalar>();
            return NbtPath.Parse(scalar.Value);
        }
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        throw new NotSupportedException();
    }
}

public class JavaUpdate
{
    public string Name;
    public JavaVersionCheck Check;
    public byte[]? Multipliers;
    public List<Rgba32>? SetBaseColors;
    public List<Rgba32>? AddBaseColors;
    public NbtCompound? MapItem;
    public NbtCompound? StructureItem;
    public NbtCompound? ChestItem;
    public NbtCompound? MapData;
    public NbtCompound? MapEntity;
    public bool? StructuresNamespace;
    public string? StructureFile;
    public string? MapFile;
    public string? LastId;
    public string? PlayerData;
    public bool? GlowFrames;
    public bool? InvisibleFrames;
}

public class BedrockUpdate
{
    public string Name;
    public BedrockVersionCheck Check;
    public NbtCompound? MapData;
    public NbtCompound? MapItem;
    public NbtCompound? ChestItem;
    public NbtCompound? StructureItem;
    public bool? GlowFrames;
}

public class BedrockVersionCheck
{
    public int[] Version;
    public bool Passes(NbtCompound leveldat)
    {
        var numbers = ReadWorldVersion(leveldat);
        if (numbers == null)
            return false;
        return IsAtLeast(numbers, Version);
    }

    internal static int[]? ReadWorldVersion(NbtCompound leveldat)
    {
        foreach (var name in new[] { "lastOpenedWithVersion", "MinimumCompatibleClientVersion" })
        {
            var list = leveldat.Get<NbtList>(name);
            if (list != null && list.Count > 0)
            {
                var parts = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                    parts[i] = list[i].IntValue;
                return parts;
            }
        }
        var inventory = leveldat.Get<NbtString>("InventoryVersion");
        if (inventory != null)
        {
            var tokens = inventory.Value.Split('.');
            var parts = new List<int>(tokens.Length);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out int n))
                    parts.Add(n);
                else
                    break;
            }
            if (parts.Count > 0)
                return parts.ToArray();
        }
        return null;
    }

    // Missing components count as 0, so 1.16 < 1.16.100 and 1.0.0 == 1.0.
    internal static bool IsAtLeast(int[] world, int[] minimum)
    {
        int n = Math.Max(world.Length, minimum.Length);
        for (int i = 0; i < n; i++)
        {
            int w = i < world.Length ? world[i] : 0;
            int m = i < minimum.Length ? minimum[i] : 0;
            if (w != m)
                return w > m;
        }
        return true;
    }
}

public class JavaVersionCheck
{
    public NbtPath? Path;
    public int? DataVersion;
    public bool Passes(NbtCompound leveldat)
    {
        if (DataVersion.HasValue)
        {
            var dataversion = leveldat.Get<NbtInt>("DataVersion");
            if (dataversion != null)
                return dataversion.Value >= DataVersion.Value;
        }
        if (Path != null)
            return Path.Traverse(leveldat).Any();
        return false;
    }
}
