using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Factories;

public static class YamlSerializerFactory
{
    private static ISerializer? _serializer;
    
    public static ISerializer Create()
    {
        if (_serializer == null)
            _serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        
        return _serializer;
    }
}