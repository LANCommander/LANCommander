using System.Text.Json;
using LANCommander.SchemaGenerator;
using LANCommander.SDK.Models.Manifest;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: LANCommander.SchemaGenerator <output-dir> [version] [base-url]");
    return 1;
}

var outputDir = args[0];
var version = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : "dev";
var baseUrl = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) ? args[2] : "https://docs.lancommander.app/schemas";

Directory.CreateDirectory(outputDir);

// One schema per LCX manifest root type. Each file is fully self-contained: it embeds `$defs`
// for every type reachable from that root (including other manifest roots it cross-references,
// e.g. Game <-> Tool), so no external `$ref` resolution is required to validate against it.
var rootTypes = new[]
{
    typeof(Game),
    typeof(Redistributable),
    typeof(Server),
    typeof(Tool),
};

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

foreach (var rootType in rootTypes)
{
    var schema = ManifestSchemaBuilder.BuildRootSchema(rootType, version, baseUrl);
    var json = schema.ToJsonString(jsonOptions);

    var path = Path.Combine(outputDir, $"{rootType.Name}.schema.json");
    File.WriteAllText(path, json + Environment.NewLine);

    Console.WriteLine($"Wrote {path}");
}

return 0;
