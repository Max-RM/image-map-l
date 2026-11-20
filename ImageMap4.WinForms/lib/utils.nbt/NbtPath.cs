using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using fNbt;
using TryashtarUtils.Utility;
using System.Globalization;
using System.Collections.Generic;

namespace TryashtarUtils.Nbt
{
    public class NbtPath
    {
        public readonly string Original;
        public readonly NbtPathNode[] Nodes;

        public NbtPath(string original, NbtPathNode[] nodes)
        {
            Original = original;
            Nodes = nodes;
        }

        public static NbtPath Parse(string path)
        {
            var nodes = new List<NbtPathNode>();
            var reader = new DirectStringReader(path);
            while (reader.CanRead() && reader.Peek() != ' ')
            {
                var node = NbtPathNode.Parse(reader);
                nodes.Add(node);
                if (reader.CanRead())
                {
                    char c = reader.Peek();
                    if (c is not (' ' or '[' or '{'))
                        reader.Expect('.');
                }
            }
            return new NbtPath(path, nodes.ToArray());
        }

        public bool TryParse(string path, out NbtPath result)
        {
            try { result = Parse(path); return true; }
            catch { result = null; return false; }
        }

        // In vanilla, there are additional functions for replacing and removing matching tags.
        // That allows you to do things like remove values from an array tag,
        //   or target an index past the end of a list for appending.
        // For now, just fetching existing matches is implemented.
        public IEnumerable<NbtTag> Traverse(NbtContainerTag start)
        {
            IEnumerable<NbtTag> list = new List<NbtContainerTag> { start };
            foreach (var node in Nodes)
            {
                list = list.SelectMany(node.Get);
            }
            return list;
        }
    }

    public abstract class NbtPathNode
    {
        public abstract IEnumerable<NbtTag> Get(NbtTag start);
        public static NbtPathNode Parse(string str) => Parse(new DirectStringReader(str));
        internal static NbtPathNode Parse(StringReader reader)
        {
            char c = reader.Peek();
            switch (c)
            {
                case '{':
                    {
                        var compound = (NbtCompound)SnbtParser.Parse(reader, false, true);
                        return new MatchCompoundNode(compound);
                    }
                case '[':
                    {
                        reader.Read();
                        char next = reader.Peek();
                        if (next == '{')
                        {
                            var compound = (NbtCompound)SnbtParser.Parse(reader, false, true);
                            reader.Expect(']');
                            return new MatchListCompoundNode(compound);
                        }
                        else if (next == ']')
                        {
                            reader.Read();
                            return AllNode.Instance;
                        }
                        else
                        {
                            int index = reader.ReadInt();
                            reader.Expect(']');
                            return new IndexNode(index);
                        }
                    }
                case '"':
                    {
                        var quoted = reader.ReadString();
                        return ReadObjectNode(reader, quoted);
                    }
            }
            var str = ReadUnquotedName(reader);
            return ReadObjectNode(reader, str);
        }

        private static NbtPathNode ReadObjectNode(StringReader reader, string str)
        {
            if (reader.CanRead() && reader.Peek() == '{')
            {
                var compound = (NbtCompound)SnbtParser.Parse(reader, false, true);
                return new MatchCompoundChildNode(str, compound);
            }
            return new NamedNode(str);
        }

        private static string ReadUnquotedName(StringReader reader)
        {
            long start = reader.Cursor;
            string name = reader.ReadWhile(IsAllowedInUnquotedName);
            if (name.Length == 0)
                throw new FormatException($"Couldn't read unquoted name at position {start}");
            return name;
        }

        private static bool IsAllowedInUnquotedName(char c)
        {
            return c is not (' ' or '"' or '[' or ']' or '.' or '{' or '}');
        }

        internal static NbtTag[] GetArrayTags(NbtArrayTag array)
        {
            if (array is NbtByteArray b)
                return b.Value.Select(x => new NbtByte(x)).ToArray();
            else if (array is NbtIntArray i)
                return i.Value.Select(x => new NbtInt(x)).ToArray();
            else if (array is NbtLongArray l)
                return l.Value.Select(x => new NbtLong(x)).ToArray();
            throw new ArgumentException();
        }

        internal static NbtTag GetArrayTag(NbtArrayTag array, int index)
        {
            if (array is NbtByteArray b)
                return new NbtByte(b.Value[index]);
            else if (array is NbtIntArray i)
                return new NbtInt(i.Value[index]);
            else if (array is NbtLongArray l)
                return new NbtLong(l.Value[index]);
            throw new ArgumentException();
        }
    }

    public class MatchCompoundNode : NbtPathNode
    {
        public readonly NbtPredicate Predicate;
        public MatchCompoundNode(NbtCompound compound)
        {
            Predicate = new NbtPredicate(compound);
        }

        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (Predicate.Matches(start))
                yield return start;
        }
    }

    public class MatchListCompoundNode : NbtPathNode
    {
        public readonly NbtPredicate Predicate;
        public MatchListCompoundNode(NbtCompound compound)
        {
            Predicate = new NbtPredicate(compound);
        }

        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (start is NbtList list)
            {
                foreach (var child in list.Tags)
                {
                    if (Predicate.Matches(child))
                        yield return child;
                }
            }
        }
    }

    public class MatchCompoundChildNode : NbtPathNode
    {
        public readonly string Name;
        public readonly NbtPredicate Predicate;
        public MatchCompoundChildNode(string name, NbtCompound compound)
        {
            Name = name;
            Predicate = new NbtPredicate(compound);
        }

        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (start is NbtCompound compound)
            {
                var tag = compound.Get(Name);
                if (Predicate.Matches(tag))
                    yield return tag;
            }
        }
    }

    public class NamedNode : NbtPathNode
    {
        public readonly string Name;
        public NamedNode(string name)
        {
            Name = name;
        }

        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (start is NbtCompound compound)
            {
                var tag = compound.Get(Name);
                if (tag != null)
                    yield return tag;
            }
        }
    }

    public class IndexNode : NbtPathNode
    {
        public readonly int Index;
        public IndexNode(int index)
        {
            Index = index;
        }

        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (start is NbtList list)
            {
                int index = Wrap(list.Count);
                if (index >= 0 && index < list.Count)
                    yield return list[index];
            }
            else if (start is NbtArrayTag array)
            {
                int index = Wrap(array.Count);
                if (index >= 0 && index < array.Count)
                    yield return GetArrayTag(array, index);
            }
        }

        private int Wrap(int count)
        {
            return Index < 0 ? count + Index : Index;
        }
    }

    public class AllNode : NbtPathNode
    {
        public static readonly AllNode Instance = new();
        private AllNode() { }
        public override IEnumerable<NbtTag> Get(NbtTag start)
        {
            if (start is NbtList list)
            {
                foreach (var child in list.Tags) { yield return child; }
            }
            else if (start is NbtArrayTag array)
            {
                foreach (var child in GetArrayTags(array)) { yield return child; }
            }
        }
    }
}
