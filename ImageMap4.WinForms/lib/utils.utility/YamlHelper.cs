using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace TryashtarUtils.Utility;

public static class YamlHelper
{
    public static YamlNode? ParseFile(string file_path)
    {
        try
        {
            using var reader = File.OpenText(file_path);
            var stream = new YamlStream();
            stream.Load(reader);
            var root = stream.Documents.SingleOrDefault()?.RootNode;
            return root;
        }
        catch
        {
            Console.WriteLine($"Failed parsing YAML file {Path.GetFullPath(file_path)}");
            throw;
        }
    }

    public static void SaveToFile(YamlNode node, string file_path)
    {
        var doc = new YamlDocument(node);
        var stream = new YamlStream(doc);
        string? dir = Path.GetDirectoryName(file_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var writer = File.CreateText(file_path);
        stream.Save(writer, false);
    }

    public static List<T>? ToList<T>(this YamlNode? node, Func<YamlNode, YamlNode, T> getter)
    {
        return ((YamlMappingNode?)node)?.Children.Select(x => getter(x.Key, x.Value)).ToList();
    }

    public static List<T>? ToList<T>(this YamlNode? node, Func<YamlNode, T> getter)
    {
        return ((YamlSequenceNode?)node)?.Children.Select(getter).ToList();
    }

    public static List<T>? ToListFromStrings<T>(this YamlNode? node, Func<string?, T> getter)
    {
        return ToList(node, x => getter(x.String()));
    }

    public static List<string>? ToStringList(this YamlNode? node)
    {
        return ToListFromStrings(node, x => x!);
    }

    public static Dictionary<string, string>? ToDictionary(this YamlNode? node)
    {
        return ToDictionary(node, x => x.String()!);
    }

    public static Dictionary<string, TValue>? ToDictionary<TValue>(this YamlNode? node,
        Func<YamlNode, TValue> value_getter)
    {
        return ToDictionary(node, x => x.String()!, value_getter);
    }

    public static Dictionary<TKey, TValue>? ToDictionary<TKey, TValue>(this YamlNode? node,
        Func<YamlNode, TKey> key_getter, Func<YamlNode, TValue> value_getter)
        where TKey : notnull
    {
        return ((YamlMappingNode?)node)?.Children.ToDictionary(
            x => key_getter(x.Key),
            x => value_getter(x.Value));
    }

    public static OutType? NullableParse<OutType>(this YamlNode? node, Func<YamlNode, OutType> parser)
        where OutType : class
    {
        return node == null ? null : parser(node);
    }

    public static OutType? NullableStructParse<OutType>(this YamlNode? node, Func<YamlNode, OutType> parser)
        where OutType : struct
    {
        return node == null ? null : parser(node);
    }

    public static OutType Parse<OutType>(this YamlNode node, Func<YamlNode, OutType> parser) where OutType : class
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));
        return parser(node);
    }

    public static T ToEnum<T>(this YamlNode? node, T def) where T : struct, Enum
    {
        return node is YamlScalarNode { Value: not null } scalar
            ? StringUtils.ParseUnderscoredEnum<T>(scalar.Value)
            : def;
    }

    public static T? ToEnum<T>(this YamlNode? node) where T : struct, Enum
    {
        return node is YamlScalarNode { Value: not null } scalar
            ? StringUtils.ParseUnderscoredEnum<T>(scalar.Value)
            : null;
    }

    public static string? String(this YamlNode? node)
    {
        return node is YamlScalarNode scalar ? scalar.Value : null;
    }

    public static int? Int(this YamlNode? node)
    {
        return node is YamlScalarNode { Value: not null } scalar ? int.Parse(scalar.Value) : null;
    }

    public static bool? Bool(this YamlNode? node)
    {
        return node is YamlScalarNode { Value: not null } scalar ? bool.Parse(scalar.Value) : null;
    }

    public static YamlNode? Go(this YamlNode? node, params string[] path)
    {
        if (node == null)
            return null;
        foreach (var item in path)
        {
            node = TryGet(node, item);
            if (node == null)
                return null;
        }

        return node;
    }

    public static YamlNode? TryGet(this YamlNode node, string key)
    {
        if (node is YamlMappingNode map && map.Children.TryGetValue(key, out var result))
            return result;
        return null;
    }
}