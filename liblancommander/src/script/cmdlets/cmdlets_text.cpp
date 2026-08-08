// Write-ReplaceContentInFile and Update-IniValue — the two config-file editors.

#include "script/cmdlets/cmdlet_defs.h"

#include "lancommander/util/path.h"

extern "C" {
#include "pico_regex.h"
}

#include <cctype>
#include <cstdio>
#include <string>
#include <vector>

namespace lancommander {
namespace cmdlets {

namespace {

// --- shared file I/O ------------------------------------------------------

bool read_text(const std::string& path, std::string* out)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return false;

    out->clear();
    char buffer[4096];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out->append(buffer, read);
    std::fclose(file);

    return true;
}

bool write_text(const std::string& path, const std::string& text)
{
    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;

    const std::size_t written =
        text.empty() ? 0 : std::fwrite(text.data(), 1, text.size(), file);
    std::fclose(file);

    return written == text.size();
}

// The .NET helper normalises / and \ to the platform separator before opening.
std::string normalise_separators(const std::string& path)
{
    std::string out = path;
    for (std::size_t i = 0; i < out.size(); ++i) {
        if (out[i] == '/' || out[i] == '\\')
            out[i] = path::separator();
    }
    return out;
}

bool equals_ignore_case(const std::string& a, const std::string& b)
{
    if (a.size() != b.size())
        return false;
    for (std::size_t i = 0; i < a.size(); ++i) {
        if (std::tolower((unsigned char)a[i]) != std::tolower((unsigned char)b[i]))
            return false;
    }
    return true;
}

std::string trim(const std::string& text)
{
    std::size_t start = 0;
    std::size_t end = text.size();
    while (start < end && std::isspace((unsigned char)text[start]))
        ++start;
    while (end > start && std::isspace((unsigned char)text[end - 1]))
        --end;
    return text.substr(start, end - start);
}

// --- Write-ReplaceContentInFile -------------------------------------------

// Expands $1..$9 in a substitution using the match's capture spans.
std::string expand_substitution(const std::string& substitution,
                                const std::string& text,
                                const PicoMatch& match)
{
    std::string out;
    out.reserve(substitution.size() + 16);

    for (std::size_t i = 0; i < substitution.size(); ++i) {
        if (substitution[i] == '$' && i + 1 < substitution.size() &&
            std::isdigit((unsigned char)substitution[i + 1])) {
            const int group = substitution[i + 1] - '0';
            if (group < match.count && match.start[group] >= 0) {
                out.append(text, (std::size_t)match.start[group],
                           (std::size_t)(match.end[group] - match.start[group]));
            }
            ++i;
            continue;
        }
        out += substitution[i];
    }

    return out;
}

std::string replace_all(const std::string& text, const std::string& pattern,
                        const std::string& substitution, bool* changed)
{
    std::string out;
    std::size_t offset = 0;
    *changed = false;

    while (offset <= text.size()) {
        PicoMatch match;
        const std::string remainder = text.substr(offset);

        if (!pico_regex_exec(pattern.c_str(), remainder.c_str(), &match))
            break;
        if (match.count < 1 || match.start[0] < 0)
            break;

        out.append(remainder, 0, (std::size_t)match.start[0]);
        out += expand_substitution(substitution, remainder, match);
        *changed = true;

        const std::size_t consumed = (std::size_t)match.end[0];
        if (consumed == (std::size_t)match.start[0]) {
            // A zero-width match would loop forever; step past one character.
            if ((std::size_t)match.start[0] < remainder.size())
                out += remainder[(std::size_t)match.start[0]];
            offset += consumed + 1;
        } else {
            offset += consumed;
        }
    }

    if (offset <= text.size())
        out.append(text, offset, std::string::npos);

    return out;
}

pico_status write_replace_content_begin(PicoStage* ctx)
{
    std::string pattern;
    std::string substitution;
    std::string file_path;

    if (!arg_string(ctx, "Pattern", 0, &pattern))
        return fail(ctx, PICO_ERR_ARG, "requires a -Pattern");
    if (!arg_string(ctx, "Substitution", 1, &substitution))
        return fail(ctx, PICO_ERR_ARG, "requires a -Substitution");
    if (!arg_string(ctx, "FilePath", 2, &file_path))
        return fail(ctx, PICO_ERR_ARG, "requires a -FilePath");

    file_path = normalise_separators(file_path);

    std::string text;
    if (!read_text(file_path, &text))
        return fail(ctx, PICO_ERR_IO, "file not found: " + file_path);

    bool changed = false;
    const std::string updated = replace_all(text, pattern, substitution, &changed);

    if (changed && !write_text(file_path, updated))
        return fail(ctx, PICO_ERR_IO, "could not write " + file_path);

    return emit_string(ctx, updated);
}

const PicoParamDef write_replace_content_params[] = {
    { "Pattern", 0, 0 }, { "Substitution", 0, 1 }, { "FilePath", 0, 2 }
};

const PicoCmdletDef write_replace_content_def = {
    "Write-ReplaceContentInFile", write_replace_content_params, 3,
    write_replace_content_begin, NULL, NULL,
    "Regex-replaces text in a file in place and returns the new contents."
};

// --- Update-IniValue ------------------------------------------------------
//
// Line-based rather than parse-and-reserialize, so comments, blank lines,
// spacing and any section this call does not touch survive byte for byte.

struct IniLine {
    std::string raw;
    bool is_section;
    std::string section_name;
    bool is_key;
    std::string key_name;

    IniLine() : is_section(false), is_key(false) {}
};

void classify(IniLine* line)
{
    const std::string text = trim(line->raw);

    if (text.empty() || text[0] == ';' || text[0] == '#')
        return;

    if (text[0] == '[') {
        const std::size_t close = text.find(']');
        if (close != std::string::npos) {
            line->is_section = true;
            line->section_name = trim(text.substr(1, close - 1));
        }
        return;
    }

    const std::size_t equals = text.find('=');
    if (equals != std::string::npos) {
        line->is_key = true;
        line->key_name = trim(text.substr(0, equals));
    }
}

std::vector<IniLine> split_lines(const std::string& text, std::string* line_ending)
{
    *line_ending = text.find("\r\n") != std::string::npos ? "\r\n" : "\n";

    std::vector<IniLine> lines;
    std::size_t start = 0;

    while (start <= text.size()) {
        std::size_t end = text.find('\n', start);
        const bool last = (end == std::string::npos);
        if (last)
            end = text.size();

        std::string raw = text.substr(start, end - start);
        if (!raw.empty() && raw[raw.size() - 1] == '\r')
            raw.erase(raw.size() - 1);

        // A trailing newline produces one empty final element, which we drop so
        // rejoining does not keep adding blank lines.
        if (last && raw.empty() && start == text.size())
            break;

        IniLine line;
        line.raw = raw;
        classify(&line);
        lines.push_back(line);

        if (last)
            break;
        start = end + 1;
    }

    return lines;
}

bool is_quoted(const std::string& text)
{
    if (text.size() < 2)
        return false;
    return (text[0] == '"' && text[text.size() - 1] == '"') ||
           (text[0] == '\'' && text[text.size() - 1] == '\'');
}

// WrapValueInQuotes is tri-state in .NET (bool?): absent means "match whatever
// the existing value did", true forces quotes, false strips them.
std::string apply_quote_wrapping(const std::string& new_value,
                                 const std::string& existing_value,
                                 bool has_wrap, bool wrap)
{
    if (new_value.empty())
        return new_value;

    const bool new_quoted = is_quoted(new_value);
    const bool existing_quoted = !existing_value.empty() && is_quoted(existing_value);

    if (!has_wrap)
        return existing_quoted ? "\"" + new_value + "\"" : new_value;

    if (wrap)
        return new_quoted ? new_value : "\"" + new_value + "\"";

    return new_quoted ? new_value.substr(1, new_value.size() - 2) : new_value;
}

std::string value_of(const IniLine& line)
{
    const std::size_t equals = line.raw.find('=');
    if (equals == std::string::npos)
        return std::string();
    return trim(line.raw.substr(equals + 1));
}

pico_status update_ini_value_begin(PicoStage* ctx)
{
    std::string section;
    std::string key;
    std::string value;
    std::string file_path;

    if (!arg_string(ctx, "Section", 0, &section))
        return fail(ctx, PICO_ERR_ARG, "requires a -Section");
    if (!arg_string(ctx, "Key", 1, &key))
        return fail(ctx, PICO_ERR_ARG, "requires a -Key");
    if (!arg_string(ctx, "Value", 2, &value))
        return fail(ctx, PICO_ERR_ARG, "requires a -Value");
    if (!arg_string(ctx, "FilePath", 3, &file_path))
        return fail(ctx, PICO_ERR_ARG, "requires a -FilePath");

    bool wrap = false;
    const bool has_wrap = arg_bool(ctx, "WrapValueInQuotes", &wrap);

    bool update_or_add = true;
    arg_bool(ctx, "UpdateOrAdd", &update_or_add);
    if (pico_bp_switch(&ctx->bp, "NoAdd"))
        update_or_add = false;

    const bool only_remove = pico_bp_switch(&ctx->bp, "OnlyRemove") != 0;
    const bool clear = pico_bp_switch(&ctx->bp, "Clear") != 0;
    const bool always_append = pico_bp_switch(&ctx->bp, "AlwaysAppend") != 0;

    long insert_index = -1;
    const bool has_insert_index = arg_long(ctx, "InsertIndex", -1, &insert_index);

    // The .NET cmdlet silently returns when the file is missing.
    if (!path::exists(file_path))
        return PICO_OK;

    std::string text;
    if (!read_text(file_path, &text))
        return fail(ctx, PICO_ERR_IO, "could not read " + file_path);

    std::string line_ending;
    std::vector<IniLine> lines = split_lines(text, &line_ending);

    // Locate the section's body: [start, end) of the lines belonging to it.
    std::size_t section_start = lines.size();
    std::size_t section_end = lines.size();
    bool found_section = false;

    for (std::size_t i = 0; i < lines.size(); ++i) {
        if (!lines[i].is_section)
            continue;

        if (!found_section && equals_ignore_case(lines[i].section_name, section)) {
            found_section = true;
            section_start = i + 1;
            section_end = lines.size();
        } else if (found_section) {
            section_end = i;
            break;
        }
    }

    if (!found_section && !always_append && !update_or_add)
        return PICO_OK;

    if (!found_section) {
        if (!lines.empty() && !trim(lines[lines.size() - 1].raw).empty()) {
            IniLine blank;
            lines.push_back(blank);
        }

        IniLine header;
        header.raw = "[" + section + "]";
        classify(&header);
        lines.push_back(header);

        section_start = lines.size();
        section_end = lines.size();
    }

    // Matching keys within the section, in order.
    std::vector<std::size_t> matches;
    for (std::size_t i = section_start; i < section_end; ++i) {
        if (lines[i].is_key && equals_ignore_case(lines[i].key_name, key))
            matches.push_back(i);
    }

    if (only_remove || clear) {
        for (std::size_t i = matches.size(); i > 0; --i) {
            lines.erase(lines.begin() + (long)matches[i - 1]);
            if (matches[i - 1] < section_end)
                --section_end;
        }
        matches.clear();
    }

    // A section usually ends with a blank line separating it from the next
    // header. New keys belong before that blank, not after it — otherwise they
    // drift out of the section they were meant for.
    std::size_t append_at = section_end;
    while (append_at > section_start && trim(lines[append_at - 1].raw).empty())
        --append_at;

    if (!only_remove) {
        // The LAST match, not the first: most engines honour the final value
        // when a key is duplicated, so that is the one worth updating.
        const bool has_match = !matches.empty();
        const std::size_t match_index = has_match ? matches[matches.size() - 1] : 0;

        const std::string existing = has_match ? value_of(lines[match_index])
                                               : std::string();
        const std::string written =
            apply_quote_wrapping(value, existing, has_wrap, wrap);

        IniLine fresh;
        fresh.raw = key + "=" + written;
        classify(&fresh);

        if (always_append || (update_or_add && !has_match)) {
            std::size_t at = append_at;
            if (has_insert_index && insert_index >= 0) {
                at = section_start + (std::size_t)insert_index;
                if (at > append_at)
                    at = append_at;
            }
            lines.insert(lines.begin() + (long)at, fresh);
        } else if (has_match) {
            lines[match_index] = fresh;

            if (has_insert_index && insert_index >= 0) {
                std::size_t at = section_start + (std::size_t)insert_index;
                if (at >= section_end)
                    at = section_end > section_start ? section_end - 1 : section_start;

                const IniLine moved = lines[match_index];
                lines.erase(lines.begin() + (long)match_index);
                if (at > match_index)
                    --at;
                lines.insert(lines.begin() + (long)at, moved);
            }
        }
    }

    std::string out;
    for (std::size_t i = 0; i < lines.size(); ++i) {
        out += lines[i].raw;
        out += line_ending;
    }

    if (!write_text(file_path, out))
        return fail(ctx, PICO_ERR_IO, "could not write " + file_path);

    return PICO_OK;   // the .NET cmdlet emits nothing
}

const PicoParamDef update_ini_value_params[] = {
    { "Section", 0, 0 },   { "Key", 0, 1 },
    { "Value", 0, 2 },     { "FilePath", 0, 3 },
    { "WrapValueInQuotes", 0, -1 }, { "UpdateOrAdd", 0, -1 },
    { "NoAdd", 1, -1 },    { "OnlyRemove", 1, -1 },
    { "Clear", 1, -1 },    { "AlwaysAppend", 1, -1 },
    { "InsertIndex", 0, -1 }
};

const PicoCmdletDef update_ini_value_def = {
    "Update-IniValue", update_ini_value_params, 11,
    update_ini_value_begin, NULL, NULL,
    "Sets, adds or removes a key in an INI section, preserving the rest of the file."
};

} // namespace

const PicoCmdletDef* cmdlet_write_replace_content_in_file()
{
    return &write_replace_content_def;
}

const PicoCmdletDef* cmdlet_update_ini_value() { return &update_ini_value_def; }

} // namespace cmdlets
} // namespace lancommander
