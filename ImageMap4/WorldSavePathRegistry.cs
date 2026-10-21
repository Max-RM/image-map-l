using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ImageMap4;

public enum WorldSavePathKind
{
    BedrockUwp,
    BedrockUwpPreview,
    BedrockUwpCloned,
    BedrockGdkShared,
    BedrockGdkUser,
    BedrockGdkPreviewShared,
    BedrockGdkPreviewUser,
    Netease,
    Netease2,
    EducationWin32,
    EducationUwp,
    EducationUwpCloned,
    OptiCraft,
    Legacy,
    JavaSaves,
    Custom,
    Other
}

public sealed class WorldSavePath : INotifyPropertyChanged
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public WorldSavePathKind Kind { get; set; }
    public bool IsCustom { get; set; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => Label;
}

// Discovers the world save folders that actually exist on this machine, so the
// user can pick them from a list instead of browsing the file system by hand.
public static class WorldSavePathRegistry
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private sealed record Template(string Label, WorldSavePathKind Kind, string RelativePath);

    private static readonly Template[] BedrockTemplates =
    {
        new("Bedrock UWP", WorldSavePathKind.BedrockUwp, @"Packages\Microsoft.MinecraftUWP_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds"),
        new("Bedrock UWP Preview", WorldSavePathKind.BedrockUwpPreview, @"Packages\Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds"),
        new("Bedrock GDK (Shared)", WorldSavePathKind.BedrockGdkShared, @"Minecraft Bedrock\Users\Shared\games\com.mojang\minecraftWorlds"),
        new("Bedrock Preview GDK (Shared)", WorldSavePathKind.BedrockGdkPreviewShared, @"Minecraft Bedrock Preview\Users\Shared\games\com.mojang\minecraftWorlds"),
        new("Bedrock (Netease)", WorldSavePathKind.Netease, @"MinecraftPE_Netease\minecraftWorlds"),
        new("Bedrock (Netease 2)", WorldSavePathKind.Netease2, @"MinecraftPC_Netease\minecraftWorlds"),
        new("Education Edition (Win32)", WorldSavePathKind.EducationWin32, @"Minecraft Education Edition\games\com.mojang\minecraftWorlds"),
        new("Education Edition (UWP)", WorldSavePathKind.EducationUwp, @"Packages\Microsoft.MinecraftEducationEdition_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds"),
        new("OptiCraft", WorldSavePathKind.OptiCraft, @"OptiCraft\games\com.mojang\minecraftWorlds"),
        new("Win32Craft or Minecraft Legends (Legacy)", WorldSavePathKind.Legacy, @"MinecraftPE\games\com.mojang\minecraftWorlds"),
    };

    public static List<WorldSavePath> GetJavaPaths(IEnumerable<string> configured)
    {
        var list = new List<WorldSavePath>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfExists(list, seen, Path.Combine(AppData, ".minecraft", "saves"), "Java (default saves)", WorldSavePathKind.JavaSaves, false);
        AddConfigured(list, seen, configured);
        return list.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<WorldSavePath> GetBedrockPaths(IEnumerable<string> configured)
    {
        var list = new List<WorldSavePath>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in BedrockTemplates)
        {
            AddIfExists(list, seen, ResolveTemplate(template.RelativePath), template.Label, template.Kind, false);
        }
        DiscoverClonedUwp(list, seen, "Minecraft", "MinecraftEducationEdition", "Bedrock UWP (cloned)", WorldSavePathKind.BedrockUwpCloned);
        DiscoverClonedUwp(list, seen, "MinecraftEducationEdition", null, "Education Edition (UWP, cloned)", WorldSavePathKind.EducationUwpCloned);
        DiscoverGdkUsers(list, seen, Path.Combine(AppData, "Minecraft Bedrock", "Users"), "Bedrock GDK", WorldSavePathKind.BedrockGdkUser);
        DiscoverGdkUsers(list, seen, Path.Combine(AppData, "Minecraft Bedrock Preview", "Users"), "Bedrock Preview GDK", WorldSavePathKind.BedrockGdkPreviewUser);
        AddConfigured(list, seen, configured);
        return list.OrderBy(x => (int)x.Kind).ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The folder used when the user has not chosen any yet, in order of preference:
    // GDK, then UWP, then Netease, then OptiCraft, then Education, then anything else.
    public static List<WorldSavePath> GetDefaultBedrockPaths()
    {
        return GetBedrockPaths(Enumerable.Empty<string>())
            .OrderBy(x => DefaultPriority(x.Kind))
            .ThenBy(x => (int)x.Kind)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int DefaultPriority(WorldSavePathKind kind) => kind switch
    {
        WorldSavePathKind.BedrockGdkShared or
        WorldSavePathKind.BedrockGdkUser or
        WorldSavePathKind.BedrockGdkPreviewShared or
        WorldSavePathKind.BedrockGdkPreviewUser => 0,
        WorldSavePathKind.BedrockUwp or
        WorldSavePathKind.BedrockUwpCloned or
        WorldSavePathKind.BedrockUwpPreview => 1,
        WorldSavePathKind.Netease or
        WorldSavePathKind.Netease2 => 2,
        WorldSavePathKind.OptiCraft => 3,
        WorldSavePathKind.EducationWin32 or
        WorldSavePathKind.EducationUwp or
        WorldSavePathKind.EducationUwpCloned => 4,
        WorldSavePathKind.Custom => 5,
        _ => 6
    };

    private static string ResolveTemplate(string relativePath)
    {
        if (relativePath.StartsWith("Packages\\", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(LocalAppData, relativePath);
        return Path.Combine(AppData, relativePath);
    }

    private static void AddConfigured(List<WorldSavePath> list, HashSet<string> seen, IEnumerable<string>? configured)
    {
        foreach (var raw in configured ?? Enumerable.Empty<string>())
        {
            string path = Environment.ExpandEnvironmentVariables(raw);
            AddIfExists(list, seen, path, "Custom folder", WorldSavePathKind.Custom, true);
        }
    }

    private static void AddIfExists(List<WorldSavePath> list, HashSet<string> seen, string? path, string label, WorldSavePathKind kind, bool custom)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        path = path.TrimEnd('\\', '/');
        if (!seen.Add(path))
            return;
        list.Add(new WorldSavePath { Label = label, Path = path, Kind = kind, IsCustom = custom });
    }

    private static void DiscoverClonedUwp(List<WorldSavePath> list, HashSet<string> seen, string mustContain, string? mustNotContain, string labelPrefix, WorldSavePathKind kind)
    {
        string packages = Path.Combine(LocalAppData, "Packages");
        if (!Directory.Exists(packages))
            return;
        foreach (var dir in Directory.GetDirectories(packages))
        {
            string name = Path.GetFileName(dir);
            if (name.IndexOf(mustContain, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (mustNotContain != null && name.IndexOf(mustNotContain, StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (name.StartsWith("Microsoft.MinecraftUWP_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.MinecraftWindowsBeta_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.MinecraftEducationEdition_", StringComparison.OrdinalIgnoreCase))
                continue;
            string worldPath = Path.Combine(dir, "LocalState", "games", "com.mojang", "minecraftWorlds");
            AddIfExists(list, seen, worldPath, labelPrefix + ": " + name, kind, false);
        }
    }

    private static void DiscoverGdkUsers(List<WorldSavePath> list, HashSet<string> seen, string usersFolder, string labelPrefix, WorldSavePathKind kind)
    {
        if (!Directory.Exists(usersFolder))
            return;
        foreach (var user in Directory.GetDirectories(usersFolder))
        {
            string name = Path.GetFileName(user);
            if (string.Equals(name, "Shared", StringComparison.OrdinalIgnoreCase))
                continue;
            string worldPath = Path.Combine(user, "games", "com.mojang", "minecraftWorlds");
            AddIfExists(list, seen, worldPath, labelPrefix + ": " + name, kind, false);
        }
    }
}
