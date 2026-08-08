#include "test_main.h"

#include "lancommander/archive/crc32_util.h"
#include "lancommander/archive/zip_archive_extractor.h"
#include "lancommander/script/cmdlets.h"
#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>
#include <vector>

using namespace lancommander;

namespace {

// Records what the cmdlets ask for and answers with canned responses, so the
// API-backed cmdlets can be tested without a server.
class FakeHttpClient : public IHttpClient {
public:
    struct Call {
        std::string method;
        std::string path;
        std::string body;
    };

    std::vector<Call> calls;

    int next_status;
    std::string next_body;
    bool download_succeeds;
    std::string download_payload;

    FakeHttpClient()
        : next_status(200), download_succeeds(true) {}

    void set_base_url(const std::string&) {}
    void set_bearer_token(const std::string&) {}

    HttpResponse get(const std::string& path)
    {
        record("GET", path, "");
        return respond();
    }

    HttpResponse post(const std::string& path, const std::string& body,
                      const std::string& = "application/json")
    {
        record("POST", path, body);
        return respond();
    }

    HttpResponse put(const std::string& path, const std::string& body,
                     const std::string& = "application/json")
    {
        record("PUT", path, body);
        return respond();
    }

    HttpResponse del(const std::string& path)
    {
        record("DELETE", path, "");
        return respond();
    }

    bool download(const std::string& path, const std::string& dest_path,
                  DownloadProgressFn = DownloadProgressFn())
    {
        record("DOWNLOAD", path, "");

        if (!download_succeeds)
            return false;

        std::FILE* file = std::fopen(dest_path.c_str(), "wb");
        if (!file)
            return false;
        if (!download_payload.empty())
            std::fwrite(download_payload.data(), 1, download_payload.size(), file);
        std::fclose(file);

        return true;
    }

    HttpResponse post_multipart_file(const std::string& path, const std::string&,
                                     const std::string&)
    {
        record("MULTIPART", path, "");
        return respond();
    }

    const Call& last() const { return calls[calls.size() - 1]; }

private:
    void record(const char* method, const std::string& path, const std::string& body)
    {
        Call call;
        call.method = method;
        call.path = path;
        call.body = body;
        calls.push_back(call);
    }

    HttpResponse respond()
    {
        HttpResponse response;
        response.status_code = next_status;
        response.body = next_body;
        return response;
    }
};

ScriptResult run(const std::string& source)
{
    PicoPoshScriptRunner runner;
    return runner.run_inline(source, "api-cmdlet-test", "", ScriptVariableList());
}

// A one-entry stored zip, built the same way test_archive.cpp does.
std::string build_zip(const std::string& name, const std::string& data)
{
    std::string out;

    Crc32 crc;
    crc.update(data.data(), data.size());
    const unsigned long checksum = crc.value();

    struct Put {
        static void v16(std::string* o, unsigned int v)
        {
            o->push_back((char)(v & 0xFF));
            o->push_back((char)((v >> 8) & 0xFF));
        }
        static void v32(std::string* o, unsigned long v)
        {
            o->push_back((char)(v & 0xFF));
            o->push_back((char)((v >> 8) & 0xFF));
            o->push_back((char)((v >> 16) & 0xFF));
            o->push_back((char)((v >> 24) & 0xFF));
        }
    };

    Put::v32(&out, 0x04034b50);
    Put::v16(&out, 20); Put::v16(&out, 0); Put::v16(&out, 0);
    Put::v16(&out, 0);  Put::v16(&out, 0);
    Put::v32(&out, checksum);
    Put::v32(&out, (unsigned long)data.size());
    Put::v32(&out, (unsigned long)data.size());
    Put::v16(&out, (unsigned int)name.size()); Put::v16(&out, 0);
    out += name;
    out += data;

    const std::size_t central = out.size();

    Put::v32(&out, 0x02014b50);
    Put::v16(&out, 20); Put::v16(&out, 20); Put::v16(&out, 0); Put::v16(&out, 0);
    Put::v16(&out, 0);  Put::v16(&out, 0);
    Put::v32(&out, checksum);
    Put::v32(&out, (unsigned long)data.size());
    Put::v32(&out, (unsigned long)data.size());
    Put::v16(&out, (unsigned int)name.size());
    Put::v16(&out, 0); Put::v16(&out, 0); Put::v16(&out, 0); Put::v16(&out, 0);
    Put::v32(&out, 0); Put::v32(&out, 0);
    out += name;

    // Size the central directory before writing the end record, not after —
    // out.size() grows as the record is appended.
    const std::size_t central_size = out.size() - central;

    Put::v32(&out, 0x06054b50);
    Put::v16(&out, 0); Put::v16(&out, 0); Put::v16(&out, 1); Put::v16(&out, 1);
    Put::v32(&out, (unsigned long)central_size);
    Put::v32(&out, (unsigned long)central);
    Put::v16(&out, 0);

    return out;
}

bool write_file(const std::string& path, const std::string& contents)
{
    path::create_directories(path::parent(path));

    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;
    std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);
    return true;
}

std::string read_file(const std::string& path)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return std::string();

    std::string out;
    char buffer[1024];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out.append(buffer, read);
    std::fclose(file);
    return out;
}

} // namespace

void test_cmdlets_api()
{
    cmdlets::register_all();

    // --- with no context, they say so rather than pretending --------------
    {
        cmdlets::clear_context();

        const ScriptResult r = run("$Return = Get-UserCustomField 'Port'\n");
        CHECK(!r.success);
        CHECK(r.error.find("no server connection is configured") != std::string::npos);
    }

    FakeHttpClient http;
    cmdlets::Context context;
    context.http = &http;
    cmdlets::set_context(context);

    // --- Get-UserCustomField ------------------------------------------------
    {
        http.calls.clear();
        http.next_status = 200;
        // The endpoint returns TypedResults.Ok(value), so the body is a JSON
        // string — quoted and escaped, not raw text.
        http.next_body = "\"27960\"";

        const ScriptResult r = run("$Return = Get-UserCustomField 'Port'\n");

        CHECK(r.success);
        CHECK_EQ(r.return_value, "27960");
        CHECK(http.calls.size() == 1);
        if (!http.calls.empty()) {
            CHECK_EQ(http.last().method, "GET");
            CHECK_EQ(http.last().path, "/api/Profile/CustomField/Port");
        }
    }

    // Escapes survive the JSON decoding.
    {
        http.next_body = "\"a \\\"quoted\\\" value\"";
        CHECK_EQ(run("$Return = Get-UserCustomField 'Notes'\n").return_value,
                 "a \"quoted\" value");
    }

    // A field that was never set answers 404, which is not an error.
    {
        http.next_status = 404;
        http.next_body = "";

        const ScriptResult r = run("$Return = Get-UserCustomField 'Missing'\n");
        CHECK(r.success);
        CHECK_EQ(r.return_value, "");
    }

    // A real failure is reported.
    {
        http.next_status = 500;
        http.next_body = "";

        const ScriptResult r = run("$Return = Get-UserCustomField 'Port'\n");
        CHECK(!r.success);
        CHECK(r.error.find("HTTP 500") != std::string::npos);
    }

    // --- Update-UserCustomField ---------------------------------------------
    {
        http.calls.clear();
        http.next_status = 200;
        http.next_body = "\"27961\"";

        const ScriptResult r =
            run("$Return = Update-UserCustomField 'Port' '27961'\n");

        CHECK(r.success);
        CHECK_EQ(r.return_value, "27961");
        CHECK(http.calls.size() == 1);
        if (!http.calls.empty()) {
            CHECK_EQ(http.last().method, "PUT");
            CHECK_EQ(http.last().path, "/api/Profile/CustomField/Port");
            // [FromBody] string on the server means the body is a JSON string.
            CHECK_EQ(http.last().body, "\"27961\"");
        }
    }

    // A value needing escaping is encoded, not sent raw.
    {
        http.calls.clear();
        http.next_body = "\"\"";
        run("$Return = Update-UserCustomField 'Notes' 'say \"hi\"'\n");
        CHECK(http.calls.size() == 1);
        if (!http.calls.empty())
            CHECK_EQ(http.last().body, "\"say \\\"hi\\\"\"");
    }

    // --- Out-PlayerAvatar ---------------------------------------------------
    {
        http.calls.clear();
        http.next_status = 200;

        // A PNG header plus a NUL and a high byte: the response body has to
        // survive as arbitrary bytes, not as text.
        std::string png;
        png += (char)0x89; png += "PNG"; png += (char)0x0D; png += (char)0x0A;
        png += (char)0x00; png += (char)0xFF;
        http.next_body = png;

        const ScriptResult r = run(
            "$a = Out-PlayerAvatar\n$Return = $a -join ','\n");

        CHECK(r.success);
        // One byte array, not one emission per byte.
        CHECK_EQ(r.return_value, "137,80,78,71,13,10,0,255");

        CHECK(http.calls.size() == 1);
        if (!http.calls.empty()) {
            CHECK_EQ(http.last().method, "GET");
            CHECK_EQ(http.last().path, "/api/Profile/Avatar");
        }
    }

    {
        http.next_status = 404;
        http.next_body = "";

        const ScriptResult r = run("$Return = Out-PlayerAvatar\n");
        CHECK(!r.success);
        CHECK(r.error.find("HTTP 404") != std::string::npos);
    }

    // --- Expand-LatestArchive -----------------------------------------------
    if (!ZipArchiveExtractor::available()) {
        std::printf("  (skipping Expand-LatestArchive: no zip backend)\n");
    } else {
        const std::string root =
            path::combine(path::temp_directory(), "lc_expand_test");
        const std::string zip_payload = build_zip("payload.txt", "extracted!");

        // A local $LatestArchivePath short-circuits the download, which is the
        // server-side packaging path.
        {
            http.calls.clear();

            const std::string local = path::combine(root, "local.zip");
            const std::string destination = path::combine(root, "local_out");
            CHECK(write_file(local, zip_payload));

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_string("LatestArchivePath", local));

            PicoPoshScriptRunner runner;
            const ScriptResult r = runner.run_inline(
                "$Return = Expand-LatestArchive -DestinationPath '" +
                    destination + "'\n",
                "expand", "", variables);

            CHECK(r.success);
            CHECK_EQ(read_file(path::combine(destination, "payload.txt")),
                     "extracted!");
            // Nothing was fetched.
            CHECK(http.calls.empty());

            path::remove_file(local);
            path::remove_file(path::combine(destination, "payload.txt"));
        }

        // With no local archive, the entity comes from $GameManifest.Id.
        {
            http.calls.clear();
            http.download_payload = zip_payload;
            http.download_succeeds = true;

            const std::string destination = path::combine(root, "download_out");

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_json(
                "GameManifest", "{\"Id\":\"abc-123\",\"Title\":\"Quake\"}"));

            PicoPoshScriptRunner runner;
            const ScriptResult r = runner.run_inline(
                "$Return = Expand-LatestArchive -DestinationPath '" +
                    destination + "'\n",
                "expand", "", variables);

            CHECK(r.success);
            CHECK_EQ(read_file(path::combine(destination, "payload.txt")),
                     "extracted!");
            CHECK(http.calls.size() == 1);
            if (!http.calls.empty()) {
                CHECK_EQ(http.last().method, "DOWNLOAD");
                CHECK_EQ(http.last().path, "/api/Games/abc-123/Download");
            }

            path::remove_file(path::combine(destination, "payload.txt"));
        }

        // An explicit -RedistributableId beats the context variable, and picks
        // the redistributable route.
        {
            http.calls.clear();
            http.download_payload = zip_payload;

            const std::string destination = path::combine(root, "redist_out");

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_json(
                "GameManifest", "{\"Id\":\"abc-123\"}"));

            PicoPoshScriptRunner runner;
            const ScriptResult r = runner.run_inline(
                "$Return = Expand-LatestArchive -DestinationPath '" + destination +
                    "' -RedistributableId 'redist-9'\n",
                "expand", "", variables);

            CHECK(r.success);
            if (!http.calls.empty())
                CHECK_EQ(http.last().path, "/api/Redistributables/redist-9/Download");

            path::remove_file(path::combine(destination, "payload.txt"));
        }

        // $Redistributable outranks $GameManifest, matching .NET's precedence.
        {
            http.calls.clear();
            http.download_payload = zip_payload;

            const std::string destination = path::combine(root, "precedence_out");

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_json(
                "GameManifest", "{\"Id\":\"game-1\"}"));
            variables.push_back(ScriptVariable::of_json(
                "Redistributable", "{\"Id\":\"redist-1\"}"));

            PicoPoshScriptRunner runner;
            runner.run_inline("Expand-LatestArchive -DestinationPath '" +
                                  destination + "' | Out-Null\n",
                              "expand", "", variables);

            if (!http.calls.empty())
                CHECK_EQ(http.last().path, "/api/Redistributables/redist-1/Download");

            path::remove_file(path::combine(destination, "payload.txt"));
        }

        // picoposh has no parameter aliases, so -Destination and -OutputPath
        // are declared separately and must actually be honoured.
        {
            http.calls.clear();
            http.download_payload = zip_payload;

            const std::string by_alias = path::combine(root, "alias_out");

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_json(
                "GameManifest", "{\"Id\":\"abc-123\"}"));

            PicoPoshScriptRunner runner;
            const ScriptResult r = runner.run_inline(
                "Expand-LatestArchive -Destination '" + by_alias +
                    "' | Out-Null\n",
                "expand", "", variables);

            CHECK(r.success);
            CHECK_EQ(read_file(path::combine(by_alias, "payload.txt")), "extracted!");

            path::remove_file(path::combine(by_alias, "payload.txt"));
        }

        // Nothing to resolve: a clear message rather than a silent no-op.
        {
            http.calls.clear();

            const ScriptResult r = run(
                "$Return = Expand-LatestArchive -DestinationPath '" +
                path::combine(root, "none") + "'\n");

            CHECK(!r.success);
            CHECK(r.error.find("could not determine what to download") !=
                  std::string::npos);
            CHECK(http.calls.empty());
        }

        // A failed download reports and leaves nothing behind.
        {
            http.calls.clear();
            http.download_succeeds = false;

            const std::string destination = path::combine(root, "failed_out");

            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_json(
                "GameManifest", "{\"Id\":\"abc-123\"}"));

            PicoPoshScriptRunner runner;
            const ScriptResult r = runner.run_inline(
                "$Return = Expand-LatestArchive -DestinationPath '" +
                    destination + "'\n",
                "expand", "", variables);

            CHECK(!r.success);
            CHECK(!path::exists(path::combine(destination, "payload.txt")));

            http.download_succeeds = true;
        }
    }

    cmdlets::clear_context();
}
