#include "test_main.h"

#include "lancommander/script/cmdlets.h"
#include "lancommander/script/picoposh_script_runner.h"

#include "util/base64.h"

#include <string>

using namespace lancommander;

namespace {

ScriptResult run_full(const std::string& source)
{
    PicoPoshScriptRunner runner;
    return runner.run_inline(source, "serialize-test", "", ScriptVariableList());
}

std::string run(const std::string& source)
{
    return run_full(source).return_value;
}

std::string decode_or_empty(const std::string& encoded)
{
    std::string out;
    if (!base64::decode(encoded, &out))
        return std::string();
    return out;
}

bool contains(const std::string& haystack, const std::string& needle)
{
    return haystack.find(needle) != std::string::npos;
}

} // namespace

void test_serialize()
{
    cmdlets::register_all();

    // --- base64 itself ------------------------------------------------------
    {
        // One vector per padding length, checked against the standard encoding.
        CHECK_EQ(base64::encode(""), "");
        CHECK_EQ(base64::encode("f"), "Zg==");
        CHECK_EQ(base64::encode("fo"), "Zm8=");
        CHECK_EQ(base64::encode("foo"), "Zm9v");
        CHECK_EQ(base64::encode("foob"), "Zm9vYg==");
        CHECK_EQ(base64::encode("fooba"), "Zm9vYmE=");
        CHECK_EQ(base64::encode("foobar"), "Zm9vYmFy");

        CHECK_EQ(decode_or_empty("Zg=="), "f");
        CHECK_EQ(decode_or_empty("Zm8="), "fo");
        CHECK_EQ(decode_or_empty("Zm9v"), "foo");
        CHECK_EQ(decode_or_empty("Zm9vYmFy"), "foobar");

        // The vector from the cmdlet documentation.
        CHECK_EQ(decode_or_empty("TmFtZTogSGVsbG8="), "Name: Hello");

        // Arbitrary bytes, not text: NULs and high bytes must survive.
        {
            std::string binary;
            for (int i = 0; i < 256; ++i)
                binary += (char)(unsigned char)i;

            std::string round;
            CHECK(base64::decode(base64::encode(binary), &round));
            CHECK(round == binary);
        }

        // Whitespace between characters is skipped, as Convert.FromBase64String
        // allows — a wrapped or indented value still decodes.
        CHECK_EQ(decode_or_empty("Zm9v\nYmFy"), "foobar");
        CHECK_EQ(decode_or_empty("  Zm9v YmFy  "), "foobar");

        // Invalid input is refused rather than producing plausible garbage.
        std::string ignored;
        CHECK(!base64::decode("not!base64", &ignored));
        CHECK(!base64::decode("Zm9vYmFy=", &ignored));   // padding after a full group
        CHECK(!base64::decode("Zm9vY", &ignored));       // truncated, unpadded
        CHECK(!base64::decode("Z===", &ignored));        // one sextet encodes nothing
        CHECK(!base64::decode("Zm9v=Ymfy", &ignored));   // data after padding
    }

    // --- the documented ConvertFrom example ---------------------------------
    //
    // Straight from LANCommander.Documentation/Scripting/Cmdlets.md, so the two
    // implementations agree on the wire format.
    {
        CHECK_EQ(run("$d = ConvertFrom-SerializedBase64 -Input 'TmFtZTogSGVsbG8='\n"
                     "$Return = $d.Name\n"),
                 "Hello");
    }

    // --- the documented ConvertTo example -----------------------------------
    {
        const std::string encoded =
            run("$obj = @{ Name = 'Hello'; Value = 42 }\n"
                "$Return = ConvertTo-SerializedBase64 -Input $obj\n");

        CHECK(!encoded.empty());

        // Decoding must give YAML the .NET launcher would read back: block
        // style, plain scalars, no JSON braces.
        const std::string yaml_text = decode_or_empty(encoded);
        CHECK(contains(yaml_text, "Name: Hello"));
        CHECK(contains(yaml_text, "Value: 42"));
        CHECK(!contains(yaml_text, "{"));
    }

    // --- round trips ---------------------------------------------------------
    {
        CHECK_EQ(run("$e = ConvertTo-SerializedBase64 -Input 'plain string'\n"
                     "$Return = ConvertFrom-SerializedBase64 -Input $e\n"),
                 "plain string");

        CHECK_EQ(run("$e = ConvertTo-SerializedBase64 -Input 42\n"
                     "$Return = ConvertFrom-SerializedBase64 -Input $e\n"),
                 "42");

        // A nested object survives with its structure intact.
        CHECK_EQ(run("$obj = @{ Outer = @{ Inner = 'deep' } }\n"
                     "$e = ConvertTo-SerializedBase64 -Input $obj\n"
                     "$d = ConvertFrom-SerializedBase64 -Input $e\n"
                     "$Return = $d.Outer.Inner\n"),
                 "deep");

        // As does an array.
        CHECK_EQ(run("$obj = @{ Items = 1,2,3 }\n"
                     "$e = ConvertTo-SerializedBase64 -Input $obj\n"
                     "$d = ConvertFrom-SerializedBase64 -Input $e\n"
                     "$Return = $d.Items[1]\n"),
                 "2");

        // Values that would be misread as YAML syntax on the way back.
        CHECK_EQ(run("$obj = @{ Tricky = 'yes: no # maybe' }\n"
                     "$e = ConvertTo-SerializedBase64 -Input $obj\n"
                     "$d = ConvertFrom-SerializedBase64 -Input $e\n"
                     "$Return = $d.Tricky\n"),
                 "yes: no # maybe");

        // A string that looks like a number must stay a string.
        CHECK_EQ(run("$obj = @{ Version = '1.32' }\n"
                     "$e = ConvertTo-SerializedBase64 -Input $obj\n"
                     "$d = ConvertFrom-SerializedBase64 -Input $e\n"
                     "$Return = $d.Version\n"),
                 "1.32");
    }

    // --- scalar typing on the way back --------------------------------------
    //
    // A plain YAML scalar carries no type, and we infer one — so a number comes
    // back as a number and arithmetic works. YamlDotNet's Deserialize<object>
    // returns strings instead, which is the one documented divergence.
    {
        CHECK_EQ(run("$d = ConvertFrom-SerializedBase64 -Input 'VmFsdWU6IDQy'\n"
                     "$Return = $d.Value + 1\n"),
                 "43");
    }

    // --- pipeline form, for both --------------------------------------------
    //
    // Both cmdlets declare ValueFromPipeline in .NET, and picoposh runs begin
    // before the first process, so an absent -Input must not be an error.
    //
    // These assert on stdout rather than $Return because picoposh cannot parse
    // `$x = <pipeline>` — `$r = $a | Write-Output` is a parse error at the '|',
    // though a bare pipeline statement is fine. See docs/PICOPOSH_GAPS.md.
    {
        // Piping into ConvertTo-Json makes the resulting object inspectable;
        // on its own it would print as the opaque "PicoObject".
        const ScriptResult from_piped = run_full(
            "'TmFtZTogSGVsbG8=' | ConvertFrom-SerializedBase64 | ConvertTo-Json\n");
        CHECK(from_piped.success);
        CHECK(contains(from_piped.output, "\"Name\":\"Hello\""));

        // ConvertTo emits a base64 string, so its stdout decodes directly.
        const ScriptResult to_piped = run_full(
            "$obj = @{ Name = 'Piped' }\n"
            "$obj | ConvertTo-SerializedBase64\n");
        CHECK(to_piped.success);
        CHECK(contains(decode_or_empty(to_piped.output), "Name: Piped"));

        // And the two compose in one pipeline.
        const ScriptResult both = run_full(
            "$obj = @{ Name = 'Both' }\n"
            "$obj | ConvertTo-SerializedBase64 | ConvertFrom-SerializedBase64 "
            "| ConvertTo-Json\n");
        CHECK(both.success);
        CHECK(contains(both.output, "\"Name\":\"Both\""));
    }

    // --- Get-SanitizedPath piped --------------------------------------------
    //
    // Regression: its begin used to fail when -Path was absent, which broke
    // piped input before process() ever ran.
    {
        const ScriptResult r = run_full(
            "'Half-Life: Opposing Force' | Get-SanitizedPath\n");

        CHECK(r.success);
        CHECK(contains(r.output, "Half-Life - Opposing Force"));
    }

    // --- malformed input is reported ----------------------------------------
    {
        const ScriptResult bad_base64 =
            run_full("$Return = ConvertFrom-SerializedBase64 -Input 'not!base64'\n");
        CHECK(!bad_base64.success);
        CHECK(contains(bad_base64.error, "not valid base64"));

        // Valid base64 whose payload is not YAML. "a: [1" -> unterminated flow.
        const std::string broken = base64::encode("a: [1\n");
        const ScriptResult bad_yaml = run_full(
            "$Return = ConvertFrom-SerializedBase64 -Input '" + broken + "'\n");
        CHECK(!bad_yaml.success);
        CHECK(contains(bad_yaml.error, "not valid YAML"));
    }
}
