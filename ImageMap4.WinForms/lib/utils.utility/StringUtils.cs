using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TryashtarUtils.Utility;

public static class StringUtils
{
    // helper for inlining pluralization in message boxes
    public static string Pluralize(int amount, string singular, string plural)
    {
        if (amount == 1)
            return $"1 {singular}";
        return $"{amount} {plural}";
    }
    public static string Pluralize(int amount, string singular) => Pluralize(amount, singular, singular + "s");

    // replaces the last space with a non-break space, preventing orphaning of the last word
    public static string DeOrphan(string? input)
    {
        if (input is null)
            return "";
        int place = input.LastIndexOf(' ');
        if (place == -1)
            return input;
        return input.Remove(place, 1).Insert(place, "\u00A0"); // non-break space
    }

    public static string FastReplace(this string str, string find, string replace, StringComparison comparison)
    {
        if (str == null)
            throw new ArgumentNullException(nameof(str));
        if (str.Length == 0)
            return str;
        if (find == null)
            throw new ArgumentNullException(nameof(find));
        if (find.Length == 0)
            throw new ArgumentException(nameof(find));

        var result = new StringBuilder(str.Length);
        bool empty_replacement = string.IsNullOrEmpty(replace);

        int found;
        int search = 0;
        while ((found = str.IndexOf(find, search, comparison)) != -1)
        {
            int chars = found - search;
            if (chars != 0)
                result.Append(str, search, chars);
            if (!empty_replacement)
                result.Append(replace);
            search = found + find.Length;
            if (search == str.Length)
                return result.ToString();
        }

        result.Append(str, search, str.Length - search);
        return result.ToString();
    }

    public static IEnumerable<string> SplitLines(string text)
    {
        using var sr = new StringReader(text);
        while (sr.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public static string TimeSpan(TimeSpan time)
    {
        return time.TotalHours < 1 ? time.ToString(@"mm\:ss\.ff") : time.ToString(@"h\:mm\:ss\.ff");
    }

    public static string MediaTimeSpan(TimeSpan time, int decimals = 0)
    {
        // create decimal part if requested
        string ending = decimals > 0 ? time.ToString(new string('F', decimals)) : "";
        // only include the dot if there will be digits after it
        if (ending != "")
            ending = "." + ending;
        if (time.TotalMinutes < 10)
            return time.ToString(@"m\:ss") + ending;
        if (time.TotalHours < 1)
            return time.ToString(@"mm\:ss") + ending;
        return time.ToString(@"h\:mm\:ss") + ending;
    }

    public static T ParseUnderscoredEnum<T>(string str) where T : struct, Enum
    {
        return (T)Enum.Parse(typeof(T), SnakeToPascal(str));
    }

    public static string PascalToSnake(string text)
    {
        if (text.Length < 2)
            return text;
        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(text[0]));
        for (int i = 1; i < text.Length; ++i)
        {
            char c = text[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static string SnakeToPascal(string text)
    {
        text = text.ToLower().Replace('_', ' ');
        var info = CultureInfo.CurrentCulture.TextInfo;
        return info.ToTitleCase(text).Replace(" ", String.Empty);
    }
}