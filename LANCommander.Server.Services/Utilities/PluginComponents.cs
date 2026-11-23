namespace LANCommander.Server.Services.Utilities;

/// <summary>Checks what plugins offer to show in the server's pages before it is shown.</summary>
public static class PluginComponents
{
    /// <summary>
    /// Whether the type can be rendered as a Razor component. Matched by name, since not every
    /// assembly that checks references the components framework.
    /// </summary>
    public static bool IsComponent(Type? type) =>
        type is { IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(i => i.FullName == "Microsoft.AspNetCore.Components.IComponent");

    /// <summary>
    /// Whether the component declares a <c>[Parameter]</c> of the name that a value of the type can be
    /// given to. Matched by name, like <see cref="IsComponent"/>.
    /// </summary>
    public static bool HasParameter(Type component, string name, Type valueType) =>
        component.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) is { CanWrite: true } property
        && property.PropertyType.IsAssignableFrom(valueType)
        && property.GetCustomAttributes(true).Any(a => a.GetType().FullName == "Microsoft.AspNetCore.Components.ParameterAttribute");

    /// <summary>Whether the text can be a page's address segment: letters, digits and <c>-</c>.</summary>
    public static bool IsSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= 64 && slug.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>Whether the text can be an extension's id in an address: letters, digits, <c>.</c>, <c>-</c> and <c>_</c>.</summary>
    public static bool IsExtensionId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 128 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
