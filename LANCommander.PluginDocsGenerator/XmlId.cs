using System.Reflection;
using System.Text;

namespace LANCommander.PluginDocsGenerator;

/// <summary>
/// Builds the member identifiers used in .NET XML documentation files (e.g. "M:Namespace.Type.Method(System.Int32)")
/// from reflection, so summaries can be looked up for a given <see cref="MemberInfo"/>.
/// See ECMA-334 / the C# spec "Processing the documentation file" for the ID string format.
/// </summary>
internal static class XmlId
{
    public static string ForType(Type type) => "T:" + TypeName(type);

    public static string ForField(FieldInfo field) => "F:" + TypeName(field.DeclaringType!) + "." + field.Name;

    public static string ForProperty(PropertyInfo property) => "P:" + TypeName(property.DeclaringType!) + "." + property.Name;

    public static string ForMethod(MethodBase method)
    {
        var sb = new StringBuilder("M:");
        sb.Append(TypeName(method.DeclaringType!));
        sb.Append('.');
        sb.Append(method.Name.Replace('.', '#'));  // constructors: ".ctor" -> "#ctor"

        if (method is MethodInfo { IsGenericMethodDefinition: true } gm)
            sb.Append("``").Append(gm.GetGenericArguments().Length);

        var parameters = method.GetParameters();
        if (parameters.Length > 0)
            sb.Append('(').Append(string.Join(",", parameters.Select(p => ParameterName(p.ParameterType)))).Append(')');

        return sb.ToString();
    }

    // Full name of a type as it appears in a T: reference (nested '+' -> '.').
    private static string TypeName(Type type) => (type.FullName ?? type.Namespace + "." + type.Name).Replace('+', '.');

    // Encoding of a type when used as a method parameter.
    private static string ParameterName(Type type)
    {
        if (type.IsByRef)
            return ParameterName(type.GetElementType()!) + "@";

        if (type.IsArray)
            return ParameterName(type.GetElementType()!) + "[]";

        if (type.IsGenericParameter)
            return (type.DeclaringMethod is not null ? "``" : "`") + type.GenericParameterPosition;

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName!.Split('`')[0].Replace('+', '.');
            var args = type.GetGenericArguments().Select(ParameterName);
            return definition + "{" + string.Join(",", args) + "}";
        }

        return TypeName(type);
    }
}
