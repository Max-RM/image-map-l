namespace ImageMap4;

public static class WorldFactory
{
    public static bool IsWorld(string folder)
    {
        return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "level.dat"));
    }

    public static bool IsBedrock(string folder)
    {
        return Directory.Exists(Path.Combine(folder, "db"));
    }

    public static World Create(string folder)
    {
        if (IsBedrock(folder))
            return new BedrockWorld(folder);
        return new JavaWorld(folder);
    }
}
