using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using fNbt;
using TryashtarUtils.Utility;
using System.Globalization;

namespace TryashtarUtils.Nbt
{
    public class SnbtParser
    {
        private static readonly Regex DOUBLE_PATTERN_NOSUFFIX = new("^([-+]?(?:[0-9]+[.]|[0-9]*[.][0-9]+)(?:e[-+]?[0-9]+)?)$", RegexOptions.IgnoreCase);
        private static readonly Regex DOUBLE_PATTERN = new("^([-+]?(?:[0-9]+[.]?|[0-9]*[.][0-9]+)(?:e[-+]?[0-9]+)?d)$", RegexOptions.IgnoreCase);
        private static readonly Regex FLOAT_PATTERN = new("^([-+]?(?:[0-9]+[.]?|[0-9]*[.][0-9]+)(?:e[-+]?[0-9]+)?f)$", RegexOptions.IgnoreCase);
        private static readonly Regex BYTE_PATTERN = new("^([-+]?(?:0|[1-9][0-9]*)b)$", RegexOptions.IgnoreCase);
        private static readonly Regex LONG_PATTERN = new("^([-+]?(?:0|[1-9][0-9]*)l)$", RegexOptions.IgnoreCase);
        private static readonly Regex SHORT_PATTERN = new("^([-+]?(?:0|[1-9][0-9]*)s)$", RegexOptions.IgnoreCase);
        private static readonly Regex INT_PATTERN = new("^([-+]?(?:0|[1-9][0-9]*))$");

        private readonly StringReader Reader;
        
        public static NbtTag Parse(string snbt, bool named)
        {
            snbt = snbt.TrimStart();
            return Parse(new DirectStringReader(snbt), named, false);
        }

        public static NbtTag Parse(StringReader reader, bool named)
        {
            return Parse(reader, named, false);
        }

        internal static NbtTag Parse(StringReader reader, bool named, bool can_trail)
        {
            var parser = new SnbtParser(reader);
            var value = named ? parser.ReadNamedValue() : parser.ReadValue();
            if (!can_trail)
                parser.Finish();
            return value;
        }

        public static bool ClassicTryParse(string snbt, bool named, out NbtTag tag)
        {
            try
            {
                tag = Parse(snbt, named);
                return true;
            }
            catch (Exception)
            {
                tag = null;
                return false;
            }
        }

        private SnbtParser(StringReader reader)
        {
            Reader = reader;
        }

        private NbtTag ReadValue()
        {
            Reader.SkipWhitespace();
            char next = Reader.Peek();
            if (next == Snbt.COMPOUND_OPEN)
                return ReadCompound();
            if (next == Snbt.LIST_OPEN)
                return ReadListLike();
            return ReadTypedValue();
        }

        private NbtTag ReadNamedValue()
        {
            string key = ReadKey();
            Expect(Snbt.NAME_VALUE_SEPARATOR);
            NbtTag value = ReadValue();
            value.Name = key;
            return value;
        }

        private void Finish()
        {
            Reader.SkipWhitespace();
            if (Reader.CanRead())
                throw new FormatException($"Trailing data found after position {Reader.Cursor}");
        }

        private NbtCompound ReadCompound()
        {
            Expect(Snbt.COMPOUND_OPEN);
            Reader.SkipWhitespace();
            var compound = new NbtCompound();
            while (Reader.CanRead() && Reader.Peek() != Snbt.COMPOUND_CLOSE)
            {
                var value = ReadNamedValue();
                compound.Add(value);
                if (!ReadSeparator())
                    break;
            }
            Expect(Snbt.COMPOUND_CLOSE);
            return compound;
        }

        private bool ReadSeparator()
        {
            Reader.SkipWhitespace();
            if (Reader.CanRead() && Reader.Peek() == Snbt.VALUE_SEPARATOR)
            {
                Reader.Read();
                Reader.SkipWhitespace();
                return true;
            }
            return false;
        }

        private string ReadKey()
        {
            Reader.SkipWhitespace();
            if (!Reader.CanRead())
                throw new FormatException($"Expected a key, but reached end of data");
            return Reader.ReadString();
        }

        private NbtTag ReadListLike()
        {
            if (Reader.CanRead(3) && !StringReader.IsQuote(Reader.Peek(1)) && Reader.Peek(2) == Snbt.ARRAY_DELIMITER)
                return ReadArray();
            return ReadList();
        }

        private NbtTag ReadArray()
        {
            Expect(Snbt.LIST_OPEN);
            char type = Reader.Read();
            Reader.Read(); // skip semicolon
            Reader.SkipWhitespace();
            if (!Reader.CanRead())
                throw new FormatException($"Expected array to end, but reached end of data");
            if (type == Snbt.BYTE_ARRAY_PREFIX)
                return ReadArray(NbtTagType.Byte);
            if (type == Snbt.LONG_ARRAY_PREFIX)
                return ReadArray(NbtTagType.Long);
            if (type == Snbt.INT_ARRAY_PREFIX)
                return ReadArray(NbtTagType.Int);
            throw new FormatException($"'{type}' is not a valid array type ({Snbt.BYTE_ARRAY_PREFIX}, {Snbt.LONG_ARRAY_PREFIX}, or {Snbt.INT_ARRAY_PREFIX})");
        }

        private NbtTag ReadArray(NbtTagType arraytype)
        {
            var list = new ArrayList();
            while (Reader.Peek() != Snbt.LIST_CLOSE)
            {
                var tag = ReadValue();
                if (arraytype != tag.TagType)
                    throw new FormatException($"Array of type {arraytype} cannot contain tags of type {tag.TagType}");
                if (arraytype == NbtTagType.Byte)
                    list.Add(tag.ByteValue);
                else if (arraytype == NbtTagType.Long)
                    list.Add(tag.LongValue);
                else
                    list.Add(tag.IntValue);
                if (!ReadSeparator())
                    break;
            }
            Expect(Snbt.LIST_CLOSE);
            if (arraytype == NbtTagType.Byte)
                return new NbtByteArray(list.Cast<byte>().ToArray());
            else if (arraytype == NbtTagType.Long)
                return new NbtLongArray(list.Cast<long>().ToArray());
            else
                return new NbtIntArray(list.Cast<int>().ToArray());
        }

        private NbtList ReadList()
        {
            Expect(Snbt.LIST_OPEN);
            Reader.SkipWhitespace();
            if (!Reader.CanRead())
                throw new FormatException($"Expected list to end, but reached end of data");
            var list = new NbtList();
            while (Reader.Peek() != Snbt.LIST_CLOSE)
            {
                var tag = ReadValue();
                list.Add(tag);
                if (!ReadSeparator())
                    break;
            }
            Expect(Snbt.LIST_CLOSE);
            return list;
        }

        private NbtTag ReadTypedValue()
        {
            Reader.SkipWhitespace();
            if (StringReader.IsQuote(Reader.Peek()))
                return new NbtString(Reader.ReadQuotedString());
            string str = Reader.ReadUnquotedString();
            if (str == "")
                throw new FormatException($"Expected typed value to be non-empty");
            return TypeTag(str);
        }

        private NbtTag TypeTag(string str)
        {
            try
            {
                string sub = str[0..^1];
                if (FLOAT_PATTERN.IsMatch(str))
                    return new NbtFloat(float.Parse(sub, NumberStyles.Float, CultureInfo.InvariantCulture));
                if (BYTE_PATTERN.IsMatch(str))
                    return new NbtByte((byte)sbyte.Parse(sub));
                if (LONG_PATTERN.IsMatch(str))
                    return new NbtLong(long.Parse(sub));
                if (SHORT_PATTERN.IsMatch(str))
                    return new NbtShort(short.Parse(sub));
                if (INT_PATTERN.IsMatch(str))
                    return new NbtInt(int.Parse(str));
                if (DOUBLE_PATTERN.IsMatch(str))
                    return new NbtDouble(double.Parse(sub, NumberStyles.Float, CultureInfo.InvariantCulture));
                if (DOUBLE_PATTERN_NOSUFFIX.IsMatch(str))
                    return new NbtDouble(double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture));
                var special = SpecialCase(str);
                if (special != null)
                    return special;
            }
            catch (FormatException)
            { }
            catch (OverflowException)
            { }
            return new NbtString(str);
        }

        private NbtTag SpecialCase(string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;
            if (text[^1] == Snbt.FLOAT_SUFFIX)
            {
                var special_float = DataUtils.TryParseSpecialFloat(text[0..^1]);
                if (special_float != null)
                    return new NbtFloat(special_float.Value);
            }
            if (text[^1] == Snbt.DOUBLE_SUFFIX)
            {
                var special_double = DataUtils.TryParseSpecialFloat(text[0..^1]);
                if (special_double != null)
                    return new NbtDouble(special_double.Value);
            }
            var special_double2 = DataUtils.TryParseSpecialDouble(text);
            if (special_double2 != null)
                return new NbtDouble(special_double2.Value);
            var special_byte = TryParseSpecialByte(text);
            if (special_byte != null)
                return new NbtByte((byte)special_byte);
            return null;
        }

        private static sbyte? TryParseSpecialByte(string value)
        {
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return 0;
            return null;
        }

        public static sbyte ParseByte(string value)
        {
            return TryParseSpecialByte(value) ??
                sbyte.Parse(value);
        }

        private void Expect(char c)
        {
            Reader.SkipWhitespace();
            Reader.Expect(c);
        }
    }
}
