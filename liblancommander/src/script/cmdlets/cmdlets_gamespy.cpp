// Edit-PatchGameSpy — repoint a game's GameSpy master server references at a
// replacement service (OpenSpy by default).
//
// Two halves. Binaries get a byte-level search-and-replace of the hostname and
// the public key, both same-length so the file layout is untouched. Text
// configs get three Unreal Engine specific edits, covering UT99 and Unreal 2.
//
// Those three edits are hand-written rather than regex-driven. When this was
// written picoposh's engine had no backslash escapes, non-greedy quantifiers or
// non-capturing groups, so the .NET patterns could not run at all. It has since
// gained all three, so a regex port is now possible — but these matchers work,
// are tested, and read more plainly than the originals, so they stay. Anything
// new of this shape should reach for the regex engine first.

#include "script/cmdlets/cmdlet_defs.h"

#include "lancommander/util/path.h"

#include <cctype>
#include <cstdio>
#include <string>
#include <vector>

namespace lancommander {
namespace cmdlets {

namespace {

// Both are 11 characters. The .NET help text and the scripting docs both say
// the hostname must be "exactly 12 characters", but the code compares against
// GAMESPY_HOSTNAME.Length — which is 11. The code is what matters.
const char* kGameSpyHostname = "gamespy.com";
const char* kOpenSpyHostname = "openspy.net";

const char* kGameSpyPublicKey =
    "BF05D63E93751AD4A59A4A7389CF0BE8A22CCDEEA1E7F12C062D6E194472EFDA"
    "5184CCECEB4FBADF5EB1D7ABFE91181453972AA971F624AF9BA8F0F82E2869FB"
    "7D44BDE8D56EE50977898F3FEE75869622C4981F07506248BD3D092E8EA05C12"
    "B2FA37881176084C8F8B8756C4722CDC57D2AD28ACD3AD85934FB48D6B2D2027";

const char* kOpenSpyPublicKey =
    "afb5818995b3708d0656a5bdd20760aee76537907625f6d23f40bf17029e5680"
    "8d36966c0804e1d797e310fedd8c06e6c4121d963863d765811fc9baeb2315c9"
    "a6eaeb125fad694d9ea4d4a928f223d9f4514533f18a5432dd0435c5c6ac8e27"
    "6cf29489cb5ac880f16b0d7832ee927d4e27d622d6a450cd1560d7fa882c6c13";

// --- small text helpers ---------------------------------------------------

char lower(char c) { return (char)std::tolower((unsigned char)c); }

// Does `text` contain `needle` at `at`, ignoring case?
bool matches_at(const std::string& text, std::size_t at, const char* needle)
{
    std::size_t i = 0;
    for (; needle[i] != '\0'; ++i) {
        if (at + i >= text.size() || lower(text[at + i]) != lower(needle[i]))
            return false;
    }
    return true;
}

std::size_t find_ci(const std::string& text, const char* needle, std::size_t from)
{
    if (from > text.size())
        return std::string::npos;

    const std::size_t len = std::string(needle).size();
    if (len == 0 || text.size() < len)
        return std::string::npos;

    for (std::size_t i = from; i + len <= text.size(); ++i) {
        if (matches_at(text, i, needle))
            return i;
    }
    return std::string::npos;
}

std::size_t skip_spaces(const std::string& text, std::size_t at)
{
    // Horizontal whitespace only: a newline would leave the current line, and
    // every pattern here is within one line.
    while (at < text.size() && (text[at] == ' ' || text[at] == '\t'))
        ++at;
    return at;
}

// Past the end of the line starting at `at`, including its newline.
std::size_t end_of_line(const std::string& text, std::size_t at)
{
    while (at < text.size() && text[at] != '\n')
        ++at;
    return at < text.size() ? at + 1 : at;
}

bool read_file(const std::string& path, std::string* out)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return false;

    out->clear();
    char buffer[8192];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out->append(buffer, read);
    std::fclose(file);

    return true;
}

bool write_file(const std::string& path, const std::string& text)
{
    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;

    const std::size_t written =
        text.empty() ? 0 : std::fwrite(text.data(), 1, text.size(), file);
    std::fclose(file);

    return written == text.size();
}

// --- glob matching --------------------------------------------------------

// `*` matches any run of characters, `?` exactly one. Case-insensitive, as
// FileSystemGlobbing is by default.
bool glob_match(const std::string& pattern, const std::string& name)
{
    std::size_t p = 0;
    std::size_t n = 0;
    std::size_t star = std::string::npos;
    std::size_t retry = 0;

    while (n < name.size()) {
        if (p < pattern.size() &&
            (pattern[p] == '?' || lower(pattern[p]) == lower(name[n]))) {
            ++p;
            ++n;
        } else if (p < pattern.size() && pattern[p] == '*') {
            star = p++;
            retry = n;
        } else if (star != std::string::npos) {
            // Backtrack: let the last '*' absorb one more character.
            p = star + 1;
            n = ++retry;
        } else {
            return false;
        }
    }

    while (p < pattern.size() && pattern[p] == '*')
        ++p;

    return p == pattern.size();
}

std::vector<std::string> split_pattern(const std::string& pattern)
{
    std::vector<std::string> segments;
    std::string current;

    for (std::size_t i = 0; i < pattern.size(); ++i) {
        if (pattern[i] == '/' || pattern[i] == '\\') {
            if (!current.empty())
                segments.push_back(current);
            current.clear();
        } else {
            current += pattern[i];
        }
    }

    if (!current.empty())
        segments.push_back(current);

    return segments;
}

// Collects files under `directory` matching the segments from `index` on.
// A "**" segment matches zero or more directory levels, so "*.dll" stays
// non-recursive while "**/*.dll" walks the tree — the same distinction
// FileSystemGlobbing draws, and why the .NET defaults only cover the top level.
void collect(const std::string& directory,
             const std::vector<std::string>& segments, std::size_t index,
             std::vector<std::string>* out, int depth)
{
    if (index >= segments.size() || depth > 32)
        return;

    Result<std::vector<path::DirectoryEntry> > entries =
        path::list_directory(directory);
    if (!entries)
        return;

    const bool last = (index + 1 == segments.size());

    if (segments[index] == "**") {
        // Zero levels: try the remaining segments right here.
        if (!last)
            collect(directory, segments, index + 1, out, depth + 1);

        for (std::size_t i = 0; i < entries.value.size(); ++i) {
            const path::DirectoryEntry& entry = entries.value[i];

            if (entry.is_directory) {
                // One or more levels: keep the "**" and descend.
                collect(path::combine(directory, entry.name), segments, index,
                        out, depth + 1);
            } else if (last) {
                out->push_back(path::combine(directory, entry.name));
            }
        }
        return;
    }

    for (std::size_t i = 0; i < entries.value.size(); ++i) {
        const path::DirectoryEntry& entry = entries.value[i];

        if (!glob_match(segments[index], entry.name))
            continue;

        if (last) {
            if (!entry.is_directory)
                out->push_back(path::combine(directory, entry.name));
        } else if (entry.is_directory) {
            collect(path::combine(directory, entry.name), segments, index + 1,
                    out, depth + 1);
        }
    }
}

void collect_matches(const std::string& root,
                     const std::vector<std::string>& patterns,
                     std::vector<std::string>* out)
{
    for (std::size_t i = 0; i < patterns.size(); ++i) {
        const std::vector<std::string> segments = split_pattern(patterns[i]);
        if (!segments.empty())
            collect(root, segments, 0, out, 0);
    }
}

// --- binary patching ------------------------------------------------------

// Every offset of `needle` in the file. Streamed with an overlap so a match
// spanning a chunk boundary is still found, without holding the whole file.
//
// The .NET equivalent bounds its inner comparison loop by the buffer length
// rather than the pattern length, so it indexes past the end of the pattern the
// moment it finds a full match. This one does not.
bool find_all(const std::string& path, const std::string& needle,
              std::vector<long>* offsets)
{
    if (needle.empty())
        return false;

    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return false;

    const std::size_t chunk = 8192;
    const std::size_t overlap = needle.size() - 1;

    std::vector<char> buffer(chunk + overlap);
    std::size_t tail = 0;
    long base = 0;

    for (;;) {
        const std::size_t read =
            std::fread(&buffer[tail], 1, chunk, file);
        if (read == 0)
            break;

        const std::size_t total = tail + read;

        if (total >= needle.size()) {
            const std::size_t limit = total - needle.size() + 1;
            for (std::size_t i = 0; i < limit; ++i) {
                std::size_t j = 0;
                while (j < needle.size() && buffer[i + j] == needle[j])
                    ++j;
                if (j == needle.size())
                    offsets->push_back(base + (long)i);
            }
        }

        tail = overlap < total ? overlap : total;
        for (std::size_t i = 0; i < tail; ++i)
            buffer[i] = buffer[total - tail + i];

        base += (long)(total - tail);
    }

    std::fclose(file);

    return true;
}

// Overwrites `replacement` at each offset. Same length as what it replaces, so
// the file's layout and size are untouched.
bool patch_at(const std::string& path, const std::vector<long>& offsets,
              const std::string& replacement)
{
    if (offsets.empty())
        return true;

    std::FILE* file = std::fopen(path.c_str(), "r+b");
    if (!file)
        return false;

    for (std::size_t i = 0; i < offsets.size(); ++i) {
        if (std::fseek(file, offsets[i], SEEK_SET) != 0) {
            std::fclose(file);
            return false;
        }
        if (std::fwrite(replacement.data(), 1, replacement.size(), file) !=
            replacement.size()) {
            std::fclose(file);
            return false;
        }
    }

    std::fclose(file);
    return true;
}

bool replace_bytes(const std::string& path, const std::string& needle,
                   const std::string& replacement)
{
    std::vector<long> offsets;
    if (!find_all(path, needle, &offsets))
        return false;

    return patch_at(path, offsets, replacement);
}

// --- the three text transformations ---------------------------------------

// 1) MasterServerAddress = master0.gamespy.com  ->  master.<hostname>
//    Covers both [UBrowserAll] and [Engine.GameEngine] ServerActors lines.
bool patch_master_server_address(std::string* text, const std::string& hostname)
{
    bool changed = false;
    std::size_t at = 0;

    for (;;) {
        const std::size_t start = find_ci(*text, "MasterServerAddress", at);
        if (start == std::string::npos)
            break;

        std::size_t cursor = skip_spaces(*text, start + 19);

        if (cursor >= text->size() || (*text)[cursor] != '=') {
            at = start + 1;
            continue;
        }

        cursor = skip_spaces(*text, cursor + 1);

        if (!matches_at(*text, cursor, "master0.gamespy.com")) {
            at = start + 1;
            continue;
        }

        const std::string replacement = "MasterServerAddress=master." + hostname;
        const std::size_t end = cursor + 19;   // length of master0.gamespy.com

        text->replace(start, end - start, replacement);
        at = start + replacement.size();
        changed = true;
    }

    return changed;
}

// 2) Inside the [UBrowserAll] block, bFallbackFactories = True -> False.
//    The block runs until the next '[', matching the .NET pattern's [^\[]*?.
bool patch_fallback_factories(std::string* text)
{
    const std::size_t section = find_ci(*text, "[UBrowserAll]", 0);
    if (section == std::string::npos)
        return false;

    std::size_t limit = text->find('[', section + 1);
    if (limit == std::string::npos)
        limit = text->size();

    std::size_t at = section;

    for (;;) {
        const std::size_t start = find_ci(*text, "bFallbackFactories", at);
        if (start == std::string::npos || start >= limit)
            return false;

        std::size_t cursor = skip_spaces(*text, start + 18);

        if (cursor >= text->size() || (*text)[cursor] != '=') {
            at = start + 1;
            continue;
        }

        cursor = skip_spaces(*text, cursor + 1);

        if (!matches_at(*text, cursor, "True")) {
            at = start + 1;
            continue;
        }

        // Only the value changes; the key and its spacing are left alone.
        text->replace(cursor, 4, "False");
        return true;
    }
}

// 3) Replace the run of MasterServerList= lines under [IpDrv.MasterServerLink]
//    with a single entry pointing at the replacement service.
bool patch_master_server_list(std::string* text, const std::string& hostname)
{
    const std::size_t section = find_ci(*text, "[IpDrv.MasterServerLink]", 0);
    if (section == std::string::npos)
        return false;

    // The header line, including its newline, is preserved.
    const std::size_t after_header = end_of_line(*text, section);

    std::size_t cursor = after_header;
    std::size_t last = after_header;

    for (;;) {
        const std::size_t line_start = skip_spaces(*text, cursor);
        if (!matches_at(*text, line_start, "MasterServerList="))
            break;

        const std::size_t line_end = end_of_line(*text, line_start);
        if (line_end == line_start)
            break;                      // no trailing newline: not a match

        // The .NET pattern requires each line to end with a newline.
        if ((*text)[line_end - 1] != '\n')
            break;

        cursor = line_end;
        last = line_end;
    }

    if (last == after_header)
        return false;                   // the (?:…)+ needs at least one line

    const std::string replacement =
        "MasterServerList=(Address=\"utmaster." + hostname + "\",Port=28902)\r\n";

    text->replace(after_header, last - after_header, replacement);

    return true;
}

bool patch_text_file(const std::string& path, const std::string& hostname)
{
    std::string text;
    if (!read_file(path, &text))
        return false;

    bool changed = false;

    changed |= patch_master_server_address(&text, hostname);
    changed |= patch_fallback_factories(&text);
    changed |= patch_master_server_list(&text, hostname);

    // Only rewrite when something actually changed, as TextFileHelper does.
    if (changed)
        return write_file(path, text);

    return true;
}

// --- parameters -----------------------------------------------------------

// A parameter that may be a single string or an array of them.
bool arg_string_list(PicoStage* ctx, const char* name, int position,
                     std::vector<std::string>* out)
{
    pico_value v;
    if (!pico_bp_named(&ctx->bp, name, &v) &&
        !(position >= 0 && pico_bp_pos(&ctx->bp, position, &v)))
        return false;

    out->clear();

    if (v.type == PICO_VT_ARRAY && v.u.a) {
        const int count = pico_array_count(v.u.a);
        for (int i = 0; i < count; ++i) {
            pico_value item;
            if (pico_array_get(v.u.a, i, &item)) {
                PicoStr* s = pico_val_to_str(item);
                if (s) {
                    out->push_back(pico_str_cstr(s));
                    pico_str_release(s);
                }
                pico_val_release(item);
            }
        }
    } else {
        PicoStr* s = pico_val_to_str(v);
        if (s) {
            out->push_back(pico_str_cstr(s));
            pico_str_release(s);
        }
    }

    pico_val_release(v);

    return !out->empty();
}

// picoposh does not parse PowerShell's `-Param a,b` array-argument syntax: the
// comma arrives as its own argument token, so `-BinariesToPatch '*.dll','*.exe'`
// binds only "*.dll" and leaves "," and "*.exe" as positionals — which then
// silently land on -Hostname and -PublicKey. The scripting documentation shows
// exactly that form, so catch it and say what to write instead.
bool has_stray_comma(PicoStage* ctx)
{
    for (int i = 0; i < ctx->bp.npos; ++i) {
        pico_value v;
        if (!pico_bp_pos(&ctx->bp, i, &v))
            continue;

        PicoStr* s = pico_val_to_str(v);
        const bool comma = s && std::string(pico_str_cstr(s)) == ",";

        if (s)
            pico_str_release(s);
        pico_val_release(v);

        if (comma)
            return true;
    }

    return false;
}

pico_status edit_patch_gamespy_begin(PicoStage* ctx)
{
    if (has_stray_comma(ctx)) {
        return fail(ctx, PICO_ERR_ARG,
                    "a bare comma list is not supported here; write an array "
                    "instead, as in -BinariesToPatch @('*.dll','*.exe')");
    }

    std::string root;
    if (!arg_string(ctx, "Path", 0, &root) || root.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Path");

    if (!path::is_directory(root))
        return fail(ctx, PICO_ERR_IO, "not a directory: " + root);

    std::string hostname = kOpenSpyHostname;
    arg_string(ctx, "Hostname", 1, &hostname);

    std::string public_key = kOpenSpyPublicKey;
    arg_string(ctx, "PublicKey", 2, &public_key);

    if (hostname.size() != std::string(kGameSpyHostname).size()) {
        char buffer[128];
        std::sprintf(buffer, "-Hostname must be exactly %u characters, to match "
                             "the length of \"gamespy.com\"",
                     (unsigned)std::string(kGameSpyHostname).size());
        return fail(ctx, PICO_ERR_ARG, buffer);
    }

    if (public_key.size() != std::string(kGameSpyPublicKey).size()) {
        char buffer[128];
        std::sprintf(buffer, "-PublicKey must be exactly %u characters, to match "
                             "the length of the original key",
                     (unsigned)std::string(kGameSpyPublicKey).size());
        return fail(ctx, PICO_ERR_ARG, buffer);
    }

    std::vector<std::string> binary_patterns;
    if (!arg_string_list(ctx, "BinariesToPatch", 3, &binary_patterns)) {
        binary_patterns.push_back("*.dll");
        binary_patterns.push_back("*.exe");
    }

    std::vector<std::string> text_patterns;
    if (!arg_string_list(ctx, "TextFilesToPatch", 4, &text_patterns)) {
        text_patterns.push_back("*.ini");
        text_patterns.push_back("*.cfg");
        text_patterns.push_back("*.conf");
    }

    std::vector<std::string> binaries;
    std::vector<std::string> texts;

    collect_matches(root, binary_patterns, &binaries);
    collect_matches(root, text_patterns, &texts);

    for (std::size_t i = 0; i < binaries.size(); ++i) {
        replace_bytes(binaries[i], kGameSpyHostname, hostname);

        // The .NET version searches for the *OpenSpy* key here rather than the
        // GameSpy one, which makes it a no-op on an unpatched binary — the
        // documented behaviour is to replace "the gamespy.com hostname and
        // public key". We search for both: the GameSpy key is what an
        // unpatched binary holds, and repeating for the OpenSpy key keeps a
        // previously patched binary re-pointable. With the default key the
        // second pass rewrites identical bytes, so it changes nothing.
        replace_bytes(binaries[i], kGameSpyPublicKey, public_key);
        replace_bytes(binaries[i], kOpenSpyPublicKey, public_key);
    }

    for (std::size_t i = 0; i < texts.size(); ++i)
        patch_text_file(texts[i], hostname);

    return PICO_OK;   // the .NET cmdlet emits nothing
}

const PicoParamDef edit_patch_gamespy_params[] = {
    { "Path", 0, 0 },             { "Hostname", 0, 1 },
    { "PublicKey", 0, 2 },        { "BinariesToPatch", 0, 3 },
    { "TextFilesToPatch", 0, 4 }
};

const PicoCmdletDef edit_patch_gamespy_def = {
    "Edit-PatchGameSpy", edit_patch_gamespy_params, 5,
    edit_patch_gamespy_begin, NULL, NULL,
    "Repoints GameSpy master server references at a replacement service."
};

} // namespace

const PicoCmdletDef* cmdlet_edit_patch_gamespy()
{
    return &edit_patch_gamespy_def;
}

} // namespace cmdlets
} // namespace lancommander
