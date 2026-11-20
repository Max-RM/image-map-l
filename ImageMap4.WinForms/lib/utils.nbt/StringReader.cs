using System;
using System.IO;
using System.Text;

namespace TryashtarUtils.Nbt
{
    public abstract class StringReader
    {
        public const char ESCAPE = '\\';
        public const char DOUBLE_QUOTE = '"';
        public const char SINGLE_QUOTE = '\'';

        public abstract long Cursor { get; protected set; }
        public abstract bool CanRead(int length = 1);
        public abstract char Peek(int offset);
        public virtual char Peek() => Peek(0);
        public abstract char Read();

        public static bool IsQuote(char c)
        {
            return c == DOUBLE_QUOTE || c == SINGLE_QUOTE;
        }

        public static bool UnquotedAllowed(char c)
        {
            return c >= '0' && c <= '9'
                || c >= 'A' && c <= 'Z'
                || c >= 'a' && c <= 'z'
                || c == '_' || c == '-'
                || c == '.' || c == '+'
                || c == '∞';
        }

        public string ReadString()
        {
            if (!CanRead())
                return String.Empty;
            char next = Peek();
            if (IsQuote(next))
            {
                Read();
                return ReadStringUntil(next);
            }
            return ReadUnquotedString();
        }

        private char ParseUnicode()
        {
            var chars = new char[] { Read(), Read(), Read(), Read() };
            int value = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                int chValue;
                if (ch <= 57 && ch >= 48)
                    chValue = ch - 48;
                else if (ch <= 70 && ch >= 65)
                    chValue = ch - 55;
                else if (ch <= 102 && ch >= 97)
                    chValue = ch - 87;
                else
                    throw new FormatException($"Invalid unicode escape sequence: \\u{chars[0]}{chars[1]}{chars[2]}{chars[3]}");
                value += chValue << ((3 - i) * 4);
            }
            return Convert.ToChar(value);
        }

        public string ReadStringUntil(char end)
        {
            var result = new StringBuilder();
            bool escaped = false;
            while (CanRead())
            {
                char c = Read();
                if (escaped)
                {
                    if (c == end || c == ESCAPE)
                    {
                        result.Append(c);
                        escaped = false;
                    }
                    else if (c == 'n')
                    {
                        result.Append('\n');
                        escaped = false;
                    }
                    else if (c == 'u')
                    {
                        result.Append(ParseUnicode());
                        escaped = false;
                    }
                    else
                    {
                        Cursor--;
                        throw new FormatException($"Tried to escape '{c}' at position {Cursor}, which is not allowed");
                    }
                }
                else if (c == ESCAPE)
                    escaped = true;
                else if (c == end)
                    return result.ToString();
                else
                    result.Append(c);
            }
            throw new FormatException($"Expected the string to end with '{end}', but reached end of data");
        }

        public string ReadWhile(Predicate<char> condition)
        {
            var builder = new StringBuilder();
            while (CanRead() && condition(Peek()))
            {
                builder.Append(Read());
            }
            return builder.ToString();
        }

        public string ReadUnquotedString()
        {
            return ReadWhile(UnquotedAllowed);
        }

        public string ReadQuotedString()
        {
            if (!CanRead())
                return String.Empty;
            char next = Peek();
            if (!IsQuote(next))
                throw new FormatException($"Expected the string at position {Cursor} to be quoted, but got '{next}'");
            Read();
            return ReadStringUntil(next);
        }

        public int ReadInt()
        {
            long start = Cursor;
            string number = ReadWhile(IsAllowedNumber);
            if (number.Length == 0)
                throw new FormatException($"Couldn't read any numeric characters starting at position {start}");
            return int.Parse(number);
        }

        public static bool IsAllowedNumber(char c)
        {
            return c >= '0' && c <= '9' || c == '.' || c == '-';
        }

        public void SkipWhitespace()
        {
            while (CanRead() && Char.IsWhiteSpace(Peek()))
            {
                Read();
            }
        }

        public void Expect(char c)
        {
            if (!CanRead())
                throw new FormatException($"Expected '{c}' at position {Cursor}, but reached end of data");
            char read = Read();
            if (read != c)
                throw new FormatException($"Expected '{c}' at position {Cursor}, but got '{read}'");
        }
    }

    public class StreamStringReader : StringReader
    {
        private readonly StreamReader BaseReader;
        public override long Cursor
        {
            get => BaseReader.BaseStream.Position;
            protected set => BaseReader.BaseStream.Position = value;
        }

        public StreamStringReader(StreamReader reader)
        {
            BaseReader = reader;
        }

        public override bool CanRead(int length = 1)
        {
            return BaseReader.BaseStream.Position + length >= BaseReader.BaseStream.Length;
        }

        public override char Peek()
        {
            return (char)BaseReader.Peek();
        }

        public override char Peek(int offset)
        {
            BaseReader.BaseStream.Position += offset;
            char result = (char)BaseReader.Peek();
            BaseReader.BaseStream.Position -= offset;
            return result;
        }

        public override char Read()
        {
            return (char)BaseReader.Read();
        }
    }

    public class DirectStringReader : StringReader
    {
        private readonly string DirectString;
        public override long Cursor { get; protected set; }

        public DirectStringReader(string str)
        {
            DirectString = str;
        }

        public override bool CanRead(int length = 1)
        {
            return Cursor + length <= DirectString.Length;
        }

        public override char Peek(int offset = 0)
        {
            return DirectString[(int)Cursor + offset];
        }

        public override char Read()
        {
            char result = Peek();
            Cursor++;
            return result;
        }
    }
}
