using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageMap.WinForms
{
    public static class WorldScanner
    {
        public static IEnumerable<string> DefaultRoots()
        {
            var roots = new List<string>();
            var local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            roots.Add(Path.Combine(local, "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftWorlds"));
            roots.Add(Path.Combine(local, "Packages", "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftWorlds"));
            var roaming = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            roots.Add(Path.Combine(roaming, ".minecraft", "saves"));
            return roots.Where(Directory.Exists);
        }

        public static IEnumerable<string> FindWorlds(string root)
        {
            if (File.Exists(Path.Combine(root, "level.dat")))
                yield return root;
            if (!Directory.Exists(root))
                yield break;
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (File.Exists(Path.Combine(dir, "level.dat")))
                    yield return dir;
            }
        }
    }
}
