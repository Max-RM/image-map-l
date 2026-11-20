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
    public class NbtPredicate
    {
        private readonly NbtTag OriginalTemplate;
        public NbtPredicate(NbtTag template)
        {
            OriginalTemplate = template;
        }

        public bool Matches(NbtTag tag)
        {
            if (tag == OriginalTemplate)
                return true;
            if (tag == null)
                return false;
            if (tag.TagType != OriginalTemplate.TagType)
                return false;
            if (tag is NbtCompound compound && OriginalTemplate is NbtCompound original_compound)
            {
                foreach (var child in original_compound.Tags)
                {
                    var existing = compound.Get(child.Name);
                    if (existing == null)
                        return false;
                    var sub = new NbtPredicate(child);
                    if (!sub.Matches(existing))
                        return false;
                }
                return true;
            }
            if (tag is NbtList list && OriginalTemplate is NbtList original_list)
            {
                if (original_list.Count == 0)
                    return list.Count == 0;
                foreach (var child in original_list.Tags)
                {
                    var sub = new NbtPredicate(child);
                    if (!list.Tags.Any(sub.Matches))
                        return false;
                }
                return true;
            }
            if (tag is NbtValueTag value && OriginalTemplate is NbtValueTag original_value)
                return value.EqualsValue(original_value);
            if (tag is NbtArrayTag array && OriginalTemplate is NbtArrayTag original_array)
                return array.EqualsArray(original_array);
            return tag.Equals(OriginalTemplate);
        }
    }
}
