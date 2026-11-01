using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using LANCommander.SDK.Plugins;

namespace LANCommander.PluginManagement;

/// <summary>What a plugin assembly declares in its <see cref="LANCommanderPluginAttribute"/>.</summary>
public sealed record PluginAssemblyInfo(
    string AssemblyName,
    string Id,
    string? MinHostVersion,
    string? MaxHostVersion,
    PluginHost Hosts);

/// <summary>
/// Reads a plugin's <see cref="LANCommanderPluginAttribute"/> from assembly metadata, without loading
/// the assembly: a package is checked before it is installed, and an assembly once loaded into a
/// host stays loaded until it restarts.
/// </summary>
public static class PluginAssemblyInspector
{
    private static readonly string AttributeNamespace = typeof(LANCommanderPluginAttribute).Namespace!;
    private static readonly string AttributeName = nameof(LANCommanderPluginAttribute);

    /// <summary>The plugin <paramref name="path"/> declares, or null when it is not a plugin assembly.</summary>
    public static PluginAssemblyInfo? Inspect(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);

            // Native libraries ship beside managed ones and carry no metadata
            if (!pe.HasMetadata)
                return null;

            var reader = pe.GetMetadataReader();

            if (!reader.IsAssembly)
                return null;

            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);

                if (IsPluginAttribute(reader, attribute))
                    return Read(reader, attribute);
            }

            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static PluginAssemblyInfo Read(MetadataReader reader, CustomAttribute attribute)
    {
        var value = attribute.DecodeValue(new NameTypeProvider());

        // The entry point is serialized as an assembly-qualified name; the loader falls back to its
        // full name when the attribute sets no Id
        var entryPoint = value.FixedArguments.Length > 0 ? value.FixedArguments[0].Value as string : null;
        entryPoint = entryPoint?.Split(',')[0].Trim();

        string? id = null, min = null, max = null;
        var hosts = PluginHost.Server | PluginHost.Launcher;

        foreach (var argument in value.NamedArguments)
        {
            switch (argument.Name)
            {
                case nameof(LANCommanderPluginAttribute.Id):
                    id = argument.Value as string;
                    break;
                case nameof(LANCommanderPluginAttribute.MinHostVersion):
                    min = argument.Value as string;
                    break;
                case nameof(LANCommanderPluginAttribute.MaxHostVersion):
                    max = argument.Value as string;
                    break;
                case nameof(LANCommanderPluginAttribute.Hosts) when argument.Value is int flags:
                    hosts = (PluginHost)flags;
                    break;
            }
        }

        return new PluginAssemblyInfo(
            reader.GetString(reader.GetAssemblyDefinition().Name),
            id ?? entryPoint ?? string.Empty,
            min,
            max,
            hosts);
    }

    private static bool IsPluginAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        var type = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => (EntityHandle)reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default,
        };

        switch (type.Kind)
        {
            case HandleKind.TypeReference:
                var reference = reader.GetTypeReference((TypeReferenceHandle)type);
                return reader.StringComparer.Equals(reference.Name, AttributeName)
                    && reader.StringComparer.Equals(reference.Namespace, AttributeNamespace);

            case HandleKind.TypeDefinition:
                var definition = reader.GetTypeDefinition((TypeDefinitionHandle)type);
                return reader.StringComparer.Equals(definition.Name, AttributeName)
                    && reader.StringComparer.Equals(definition.Namespace, AttributeNamespace);

            default:
                return false;
        }
    }

    /// <summary>
    /// Decodes attribute arguments with types as names. The attribute's only enum is
    /// <see cref="PluginHost"/>, an <see cref="int"/>.
    /// </summary>
    private sealed class NameTypeProvider : ICustomAttributeTypeProvider<string>
    {
        private const string SystemType = "System.Type";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetSystemType() => SystemType;

        public string GetSZArrayType(string elementType) => elementType + "[]";

        // Full names, so the constructor's System.Type parameter is recognised by IsSystemType
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var definition = reader.GetTypeDefinition(handle);

            return FullName(reader.GetString(definition.Namespace), reader.GetString(definition.Name));
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var reference = reader.GetTypeReference(handle);

            return FullName(reader.GetString(reference.Namespace), reader.GetString(reference.Name));
        }

        private static string FullName(string @namespace, string name) =>
            @namespace.Length == 0 ? name : $"{@namespace}.{name}";

        public string GetTypeFromSerializedName(string name) => name;

        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

        public bool IsSystemType(string type) => type == SystemType;
    }
}
