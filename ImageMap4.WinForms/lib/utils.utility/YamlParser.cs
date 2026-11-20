using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using YamlDotNet.RepresentationModel;

namespace TryashtarUtils.Utility;

public static class YamlParser
{
    public static T? Parse<T>(YamlNode node)
    {
        return (T?)Parse(node, typeof(T));
    }

    public static YamlNode Serialize<T>(T item)
    {
        return SerializeObject(item!);
    }

    private static YamlNode SerializeObject(object obj)
    {
        // primitives
        if (obj is string s)
            return new YamlScalarNode(s);
        var type = obj.GetType();
        if (type.IsEnum)
            return new YamlScalarNode(StringUtils.PascalToSnake(((Enum)obj).ToString()));
        if (type.IsPrimitive)
            return new YamlScalarNode(obj.ToString());
        // actual dictionary
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            var node = new YamlMappingNode();
            var dict = (IDictionary)obj;
            foreach (var key in dict.Keys)
            {
                node.Add(Serialize(key), Serialize(dict[key]));
            }

            return node;
        }

        // actual list
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            var node = new YamlSequenceNode();
            var list = (IEnumerable)obj;
            foreach (var item in list)
            {
                node.Add(Serialize(item));
            }

            return node;
        }

        // special case
        var root = FindRoot(type);
        if (root != null)
            return Serialize(GetVal(root, obj));
        var serializer = FindSerializer(type);
        if (serializer != null)
        {
            var serialized = GetVal(serializer, obj);
            if (serialized is YamlNode sn)
                return sn;
            return Serialize(serialized);
        }

        // member-wise object
        var result = new YamlMappingNode();
        foreach (var member in type.GetFields(PublicBinding).Cast<MemberInfo>()
                     .Concat(type.GetProperties(PublicBinding)))
        {
            var val = GetVal(member, obj);
            if (val != null)
                result.Add(ConvertName(member), Serialize(val));
        }

        return result;
    }

    private const BindingFlags AllBinding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags PublicBinding = BindingFlags.Instance | BindingFlags.Public;

    // methods to generalize shared concepts across MemberInfo
    private static object? GetVal(MemberInfo info, object obj)
    {
        if (info is FieldInfo f)
            return f.GetValue(obj);
        if (info is PropertyInfo p)
            return p.GetValue(obj);
        if (info is MethodInfo m)
            return m.Invoke(obj, null);
        throw new InvalidOperationException();
    }

    private static void SetVal(MemberInfo info, object obj, object? value)
    {
        if (info is FieldInfo f)
            f.SetValue(obj, value);
        else if (info is PropertyInfo p)
            p.SetValue(obj, value);
        else
            throw new InvalidOperationException();
    }

    private static Type TypeOf(MemberInfo info)
    {
        if (info is FieldInfo f)
            return f.FieldType;
        if (info is PropertyInfo p)
            return p.PropertyType;
        if (info is MethodInfo m)
            return m.ReturnType;
        throw new InvalidOperationException();
    }

    private static MemberInfo? FindRoot(Type type)
    {
        var fields = type.GetFields(AllBinding);
        var r_field = WithAttribute<FieldInfo, RootAttribute>(fields);
        if (r_field != null)
            return r_field;
        var properties = type.GetProperties(AllBinding);
        var r_prop = WithAttribute<PropertyInfo, RootAttribute>(properties);
        if (r_prop != null)
            return r_prop;
        return null;
    }

    private static MemberInfo? FindSerializer(Type type)
    {
        var fields = type.GetFields(AllBinding);
        var s_field = WithAttribute<FieldInfo, SerializerAttribute>(fields);
        if (s_field != null)
            return s_field;
        var properties = type.GetProperties(AllBinding);
        var s_prop = WithAttribute<PropertyInfo, SerializerAttribute>(properties);
        if (s_prop != null)
            return s_prop;
        var methods = type.GetMethods(AllBinding);
        var s_method = WithAttribute<MethodInfo, SerializerAttribute>(methods);
        if (s_method != null)
            return s_method;
        return null;
    }

    private static MethodBase? FindParser(Type type)
    {
        var constructors = type.GetConstructors(AllBinding);
        var p_cons = WithAttribute<ConstructorInfo, ParserAttribute>(constructors);
        if (p_cons != null)
            return p_cons;
        var methods = type.GetMethods(AllBinding);
        var p_method = WithAttribute<MethodInfo, ParserAttribute>(methods);
        if (p_method != null)
            return p_method;
        return null;
    }

    private static object? Parse(YamlNode node, Type type)
    {
        // assertion, without this we'll try to member-wise construct System.Object which is a sign something is wrong
        if (type == typeof(object))
            throw new InvalidOperationException();
        // primitives
        if (type == typeof(string))
            return ((YamlScalarNode)node).Value;
        if (type.IsEnum)
        {
            var text = ((YamlScalarNode)node).Value;
            return text == null ? null : Enum.Parse(type, StringUtils.SnakeToPascal(text));
        }

        if (type.IsPrimitive)
        {
            var text = ((YamlScalarNode)node).Value;
            return text == null ? null : TypeDescriptor.GetConverter(type).ConvertFrom(text);
        }

        // special case
        var parser = FindParser(type);
        object? result;
        if (parser != null)
        {
            // allow parsers to parse node directly, or convert it to another type first
            var param_type = parser.GetParameters()[0].ParameterType;
            object?[] parameters;
            if (typeof(YamlNode).IsAssignableFrom(param_type))
                parameters = new object?[] { node };
            else
                parameters = new object?[] { Parse(node, param_type) };
            // if parser is constructor, use it
            if (parser is ConstructorInfo c)
                return c.Invoke(parameters);
            // otherwise, construct ourselves then call parser
            result = Activator.CreateInstance(type);
            parser.Invoke(result, parameters);
            return result;
        }

        if (type.IsArray)
            result = Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GetElementType()!));
        else
            result = Activator.CreateInstance(type);
        if (result == null)
            return null;
        var root = FindRoot(type);
        if (root != null)
        {
            SetVal(root, result, Parse(node, TypeOf(root)));
            return result;
        }

        // actual dictionary
        if (result is IDictionary dict)
        {
            var args = type.GetGenericArguments();
            foreach (var pair in (YamlMappingNode)node)
            {
                var k = Parse(pair.Key, args[0]);
                if (k != null)
                    dict.Add(k, Parse(pair.Value, args[1]));
            }

            return dict;
        }

        // actual list
        var collection_type = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>));
        if (collection_type != null)
        {
            dynamic list = result;
            var arg = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            foreach (var item in (YamlSequenceNode)node)
            {
                dynamic? parsed = Parse(item, arg);
                list.Add(parsed);
            }

            if (type.IsArray)
                return list.ToArray();
            return list;
        }

        // member-wise object
        var map = (YamlMappingNode)node;
        var unused_nodes = map.Children.Keys.ToHashSet();
        var optional = type.GetCustomAttribute<OptionalFieldsAttribute>();
        var fields = type.GetFields(PublicBinding).Cast<MemberInfo>()
            .Concat(type.GetProperties(PublicBinding).Where(x => x.CanWrite));
        foreach (var field in fields)
        {
            string name = ConvertName(field);
            var subnode = map.TryGet(name);
            if (subnode == null)
            {
                if (optional == null && Nullable.GetUnderlyingType(TypeOf(field)) != null)
                    throw new InvalidDataException($"While parsing {type.Name}, field {name} was missing!");
                if (optional != null && optional.InitializeWhenNull)
                    SetVal(field, result, Activator.CreateInstance(TypeOf(field)));
                continue;
            }

            SetVal(field, result, Parse(subnode, TypeOf(field)));
            unused_nodes.Remove(name);
        }

        if (unused_nodes.Count > 0)
            throw new InvalidDataException(
                $"While parsing {type.Name}, found unused nodes: {String.Join(", ", unused_nodes)}");
        return result;
    }

    private static T? WithAttribute<T, U>(IEnumerable<T> members) where T : MemberInfo where U : Attribute
    {
        foreach (var member in members)
        {
            var att = member.GetCustomAttribute<U>();
            if (att != null)
                return member;
        }

        return null;
    }

    private static string ConvertName(MemberInfo member)
    {
        return StringUtils.PascalToSnake(member.Name);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class OptionalFieldsAttribute : Attribute
    {
        public readonly bool InitializeWhenNull;

        public OptionalFieldsAttribute(bool init_nulls = false)
        {
            InitializeWhenNull = init_nulls;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RootAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor)]
    public class ParserAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class SerializerAttribute : Attribute
    {
    }
}