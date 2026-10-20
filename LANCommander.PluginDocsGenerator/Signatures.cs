using System.Reflection;

namespace LANCommander.PluginDocsGenerator;

/// <summary>
/// Renders human-readable C#-style signatures for members, used in the API reference output.
/// </summary>
internal static class Signatures
{
    public static string Property(PropertyInfo property)
    {
        var accessors = property switch
        {
            { CanRead: true, SetMethod.IsPublic: true } when IsInit(property) => "{ get; init; }",
            { CanRead: true, SetMethod.IsPublic: true } => "{ get; set; }",
            { CanRead: true } => "{ get; }",
            _ => "{ set; }",
        };

        return $"{Friendly(property.PropertyType)} {property.Name} {accessors}";
    }

    public static string Method(MethodInfo method)
    {
        var generics = method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">"
            : "";

        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{Friendly(p.ParameterType)} {p.Name}"));

        return $"{Friendly(method.ReturnType)} {method.Name}{generics}({parameters})";
    }

    private static bool IsInit(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        return setMethod is not null
            && setMethod.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
    }

    public static string Friendly(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Friendly(underlying) + "?";

        if (type.IsByRef)
            return Friendly(type.GetElementType()!);

        if (type.IsArray)
            return Friendly(type.GetElementType()!) + "[]";

        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsGenericType)
        {
            var name = type.Name.Split('`')[0];
            var args = string.Join(", ", type.GetGenericArguments().Select(Friendly));
            return $"{name}<{args}>";
        }

        return Aliases.TryGetValue(type.FullName ?? "", out var alias) ? alias : type.Name;
    }

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["System.Void"] = "void",
        ["System.Object"] = "object",
        ["System.String"] = "string",
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
    };
}
