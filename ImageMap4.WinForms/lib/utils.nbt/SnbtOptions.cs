using fNbt;
using System;
using System.Text.RegularExpressions;

namespace TryashtarUtils.Nbt
{
    public class SnbtOptions
    {
        public Predicate<NbtTag> ShouldIndent = x => false;
        public Predicate<NbtTag> ShouldSpace = x => true;
        public bool IsJsonLike = false;
        public Predicate<string> ShouldQuoteKeys = x => !StringRegex.IsMatch(x);
        public Predicate<string> ShouldQuoteStrings = x => true;
        public QuoteMode KeyQuoteMode = QuoteMode.Automatic;
        public QuoteMode StringQuoteMode = QuoteMode.Automatic;
        public bool NumberSuffixes = true;
        public bool ArrayPrefixes = true;
        public INewlineHandler NewlineHandling = EscapeHandler.Instance;
        public string Indentation = "    ";

        public SnbtOptions Expanded()
        {
            this.ShouldIndent = x => true;
            this.ShouldSpace = x => true;
            return this;
        }

        public SnbtOptions WithHandler(INewlineHandler handler)
        {
            this.NewlineHandling = handler;
            return this;
        }

        private static readonly Regex StringRegex = new("^[A-Za-z0-9._+-]+$", RegexOptions.Compiled);

        public static SnbtOptions Default => new();
        public static SnbtOptions DefaultExpanded => Default.Expanded();

        public static SnbtOptions JsonLike => new()
        {
            ShouldIndent = x => false,
            ShouldSpace = x => false,
            IsJsonLike = true,
            ShouldQuoteKeys = x => true,
            ShouldQuoteStrings = x => x != "null",
            KeyQuoteMode = QuoteMode.DoubleQuotes,
            StringQuoteMode = QuoteMode.DoubleQuotes,
            NumberSuffixes = false,
            ArrayPrefixes = false,
            NewlineHandling = EscapeHandler.Instance
        };
        public static SnbtOptions JsonLikeExpanded => JsonLike.Expanded();

        public static SnbtOptions Preview => new()
        {
            ShouldIndent = x => false,
            ShouldSpace = x => true,
            ShouldQuoteKeys = x => false,
            ShouldQuoteStrings = x => false,
            NumberSuffixes = false,
            ArrayPrefixes = false,
            NewlineHandling = new ReplaceHandler("⏎")
        };

        public static SnbtOptions MultilinePreview => Preview.WithHandler(IgnoreHandler.Instance);
    }

    public enum QuoteMode
    {
        Automatic,
        DoubleQuotes,
        SingleQuotes
    }

    public interface INewlineHandler
    {
        string Handle();
    }

    public class IgnoreHandler : INewlineHandler
    {
        public static readonly IgnoreHandler Instance = new();
        public string Handle() => Environment.NewLine;
    }

    public class EscapeHandler : INewlineHandler
    {
        public static readonly EscapeHandler Instance = new();
        public string Handle() => Snbt.STRING_ESCAPE + "n";
    }

    public class ReplaceHandler : INewlineHandler
    {
        public readonly string Replacement;
        public ReplaceHandler(string replacement)
        {
            Replacement = replacement;
        }
        public string Handle() => Replacement;
    }
}
