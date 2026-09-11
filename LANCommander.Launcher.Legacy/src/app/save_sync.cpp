#include "app/save_sync.h"

#include "app/fs.h"
#include "app/game_metadata.h"
#include "app/logger.h"
#include "app/script_host.h"
#include "app/zip_io.h"

#include <lancommander/clients/game_client.h>
#include <lancommander/clients/save_client.h>
#include <lancommander/manifest_helper.h>
#include <lancommander/script/script_helper.h>
#include <lancommander/util/path.h>

#include "miniz.h"

extern "C" {
#include "pico_regex.h"
}

#include <cstdio>
#include <cstring>
#include <string>
#include <utility>
#include <vector>

namespace launcher
{

    using lancommander::GameManifest;
    using lancommander::ManifestSavePath;
    using lancommander::SavePathEntry;

    namespace
    {
        // Everything inside an archive is forward-slashed, on every platform.
        std::string to_archive_slashes(const std::string &s)
        {
            std::string out = s;
            for (std::size_t i = 0; i < out.size(); ++i)
                if (out[i] == '\\')
                    out[i] = '/';
            return out;
        }

        void trim_leading_slashes(std::string &s)
        {
            std::size_t at = 0;
            while (at < s.size() && (s[at] == '/' || s[at] == '\\'))
                ++at;
            if (at)
                s.erase(0, at);
        }

        bool starts_with(const std::string &s, const std::string &prefix)
        {
            return s.size() >= prefix.size() &&
                   s.compare(0, prefix.size(), prefix) == 0;
        }

        // Every file under `dir`, as paths relative to it with forward
        // slashes. Directories are descended, not listed.
        void walk_files(const std::string &dir, const std::string &prefix,
                        std::vector<std::string> *out)
        {
            lancommander::Result<std::vector<lancommander::path::DirectoryEntry> >
                listing = lancommander::path::list_directory(dir);

            if (!listing)
                return;

            for (std::size_t i = 0; i < listing.value.size(); ++i)
            {
                const lancommander::path::DirectoryEntry &entry = listing.value[i];
                const std::string child = lancommander::path::combine(dir, entry.name);
                const std::string rel =
                    prefix.empty() ? entry.name : (prefix + "/" + entry.name);

                if (entry.is_directory)
                    walk_files(child, rel, out);
                else
                    out->push_back(rel);
            }
        }

        std::string parent_of(const std::string &path)
        {
            const std::string::size_type cut = path.find_last_of("/\\");
            return cut == std::string::npos ? std::string()
                                            : path.substr(0, cut);
        }

        // --- JSON for the archive's own manifest ----------------------------
        //
        // Built as JSON and handed to the SDK's YAML writer rather than
        // hand-rolled as YAML: the paths in here contain backslashes, colons
        // and spaces, and getting YAML quoting subtly wrong would produce an
        // archive the .NET launcher silently misreads.

        std::string json_escape(const std::string &s)
        {
            std::string out;
            for (std::size_t i = 0; i < s.size(); ++i)
            {
                const char c = s[i];
                switch (c)
                {
                case '"':  out += "\\\""; break;
                case '\\': out += "\\\\"; break;
                case '\n': out += "\\n";  break;
                case '\r': out += "\\r";  break;
                case '\t': out += "\\t";  break;
                default:
                    if ((unsigned char)c < 0x20)
                    {
                        char esc[8];
                        sprintf(esc, "\\u%04X", (unsigned)(unsigned char)c);
                        out += esc;
                    }
                    else
                    {
                        out += c;
                    }
                }
            }
            return out;
        }

        std::string json_field(const char *name, const std::string &value)
        {
            return std::string("\"") + name + "\":\"" + json_escape(value) + "\"";
        }

        // The comma-separated form a [Flags] enum serialises to, which is one
        // of the three shapes parse_runtime_platform accepts.
        std::string platform_names(int platforms)
        {
            std::string out;

            const struct { int flag; const char *name; } known[] = {
                { lancommander::RuntimePlatform_Windows, "Windows" },
                { lancommander::RuntimePlatform_Linux,   "Linux" },
                { lancommander::RuntimePlatform_macOS,   "macOS" }
            };

            for (std::size_t i = 0; i < sizeof(known) / sizeof(known[0]); ++i)
            {
                if ((platforms & known[i].flag) == 0)
                    continue;
                if (!out.empty())
                    out += ", ";
                out += known[i].name;
            }

            return out;
        }

        // The manifest that goes into the archive. Only what the restore side
        // reads: the game's identity and its save paths, each carrying the
        // entries that were actually packed.
        std::string archive_manifest_json(const GameManifest &manifest,
                                          const std::vector<ManifestSavePath> &paths)
        {
            std::string json = "{";
            json += json_field("Id", manifest.id) + ",";
            json += json_field("Title", manifest.title) + ",";
            json += json_field("Version", manifest.version) + ",";
            json += "\"SavePaths\":[";

            for (std::size_t i = 0; i < paths.size(); ++i)
            {
                const ManifestSavePath &sp = paths[i];

                if (i)
                    json += ",";
                json += "{";
                json += json_field("Id", sp.id) + ",";
                json += json_field("Path", sp.path) + ",";
                json += json_field("WorkingDirectory", sp.working_directory) + ",";
                json += std::string("\"Type\":\"") + (sp.is_file ? "File" : "Registry") + "\",";
                json += std::string("\"IsRegex\":") + (sp.is_regex ? "true" : "false") + ",";
                json += json_field("Platforms", platform_names(sp.platforms)) + ",";
                json += "\"Entries\":[";

                for (std::size_t e = 0; e < sp.entries.size(); ++e)
                {
                    if (e)
                        json += ",";
                    json += "{";
                    json += json_field("ArchivePath", sp.entries[e].archive_path) + ",";
                    json += json_field("ActualPath", sp.entries[e].actual_path);
                    json += "}";
                }

                json += "]}";
            }

            json += "]}";

            return json;
        }

        bool read_manifest_from_disk(const std::string &install_dir,
                                     const std::string &game_id,
                                     GameManifest *out)
        {
            lancommander::Result<GameManifest> manifest =
                lancommander::manifest::read(install_dir, game_id);

            if (!manifest)
                return false;

            *out = manifest.value;
            return true;
        }

        void run_save_script(const SaveSyncContext &ctx, lancommander::ScriptType type)
        {
            if (!ctx.scripts)
                return;

            ScriptTarget target;
            target.game_id = ctx.game_id;
            target.title = ctx.title;
            target.install_dir = ctx.install_dir;
            target.server_address = ctx.server_address;
            target.default_install_dir = ctx.default_install_dir;
            target.player_alias = game_player_alias(ctx.install_dir, ctx.game_id);
            target.on_ui_thread = ctx.on_ui_thread;

            // Absent or platform-gated is a no-op, so this is safe to call for
            // every game rather than only those that have one.
            ctx.scripts->run(type, target, NULL);
        }
    } // namespace

    // -----------------------------------------------------------------------
    // Save path resolution
    // -----------------------------------------------------------------------

    std::vector<SavePathEntry> save_path_entries(const ManifestSavePath &save_path,
                                                 const std::string &install_dir)
    {
        std::vector<SavePathEntry> entries;

        std::string working = expand_game_path(save_path.working_directory,
                                               install_dir);
        if (working.empty())
            working = expand_game_path("{InstallDir}", install_dir);

        const std::string deflated_working =
            to_archive_slashes(deflate_game_path(working, install_dir));

        std::vector<std::string> locals;

        if (save_path.is_regex)
        {
            // Every file under the working directory whose path relative to it
            // matches. Matched against the RELATIVE path, as the .NET side
            // does, so a pattern is not accidentally anchored to wherever the
            // game happens to be installed.
            std::vector<std::string> relative;
            walk_files(working, std::string(), &relative);

            for (std::size_t i = 0; i < relative.size(); ++i)
            {
                if (pico_regex_match(save_path.path.c_str(), relative[i].c_str()))
                    locals.push_back(lancommander::path::combine(working, relative[i]));
            }
        }
        else
        {
            // One entry whether or not it exists yet: on a fresh install the
            // file is what a download is about to create.
            locals.push_back(lancommander::path::combine(
                working, expand_game_path(save_path.path, install_dir)));
        }

        for (std::size_t i = 0; i < locals.size(); ++i)
        {
            SavePathEntry entry;
            entry.actual_path =
                to_archive_slashes(deflate_game_path(locals[i], install_dir));

            std::string archive = entry.actual_path;
            if (!deflated_working.empty() && starts_with(archive, deflated_working))
                archive = archive.substr(deflated_working.size());
            trim_leading_slashes(archive);

            entry.archive_path = archive;

            if (!entry.archive_path.empty())
                entries.push_back(entry);
        }

        return entries;
    }

    // -----------------------------------------------------------------------
    // Upload
    // -----------------------------------------------------------------------

    SaveSyncResult save_upload(const SaveSyncContext &ctx)
    {
        SaveSyncResult result;

        if (!ctx.saves || ctx.game_id.empty() || ctx.install_dir.empty())
        {
            result.error = "nothing to upload";
            result.ok = true;
            return result;
        }

        GameManifest manifest;
        if (!read_manifest_from_disk(ctx.install_dir, ctx.game_id, &manifest))
        {
            result.error = "the game has no manifest on disk";
            return result;
        }

        // Before anything is read off disk: this is the game's chance to write
        // out whatever it was holding, and the SDK never got round to calling
        // it (ScriptType.SaveUpload is declared there and executed nowhere).
        run_save_script(ctx, lancommander::ScriptType::SaveUpload);

        // Resolve first, pack second: an empty result means the game wrote no
        // saves, and uploading an empty archive over a good one is how a
        // player loses a campaign.
        std::vector<ManifestSavePath> packed;
        std::vector<std::pair<std::string, std::string> > files; // archive, local

        for (std::size_t i = 0; i < manifest.save_paths.size(); ++i)
        {
            ManifestSavePath sp = manifest.save_paths[i];

            if (!sp.is_file)
                continue; // registry save paths are not handled here
            if (!lancommander::script::supports_current_runtime(sp.platforms))
                continue;

            const std::vector<SavePathEntry> entries =
                save_path_entries(sp, ctx.install_dir);

            std::vector<SavePathEntry> present;

            for (std::size_t e = 0; e < entries.size(); ++e)
            {
                const std::string local =
                    expand_game_path(entries[e].actual_path, ctx.install_dir);

                if (fs_exists(local))
                {
                    present.push_back(entries[e]);
                    files.push_back(std::make_pair(
                        "Files/" + sp.id + "/" + entries[e].archive_path, local));
                    continue;
                }

                // A save path can name a directory; everything under it goes
                // in, keeping its shape.
                std::vector<std::string> under;
                walk_files(local, std::string(), &under);

                if (under.empty())
                    continue;

                present.push_back(entries[e]);

                for (std::size_t u = 0; u < under.size(); ++u)
                {
                    files.push_back(std::make_pair(
                        "Files/" + sp.id + "/" + entries[e].archive_path + "/" + under[u],
                        lancommander::path::combine(local, under[u])));
                }
            }

            if (present.empty())
                continue;

            sp.entries = present;
            packed.push_back(sp);
        }

        if (files.empty())
        {
            result.ok = true;
            result.had_anything = false;
            log_info("No save files to upload for %s", ctx.title.c_str());
            return result;
        }

        const std::string temp_zip = fs_temp_file("lcs");
        if (temp_zip.empty())
        {
            result.error = "could not create a temporary file";
            return result;
        }

        {
            mz_zip_archive zip;
            ZipFile zip_io;

            if (!zip_open_write(&zip, &zip_io, temp_zip))
            {
                fs_remove(temp_zip);
                result.error = "could not create the save archive";
                return result;
            }

            for (std::size_t i = 0; i < files.size(); ++i)
            {
                if (!zip_add_file(&zip, files[i].first, files[i].second))
                {
                    log_warn("Could not add %s to the save archive",
                             files[i].second.c_str());
                    continue;
                }
                result.entries++;
            }

            // The manifest goes in last but sits at the archive root, and is
            // what tells the restore side where each file belongs.
            const std::string json = archive_manifest_json(manifest, packed);
            const std::string temp_manifest = fs_temp_file("lcm");
            bool manifest_written = false;

            if (!temp_manifest.empty())
            {
                lancommander::Result<std::string> written =
                    lancommander::manifest::write_json(temp_manifest, json);

                if (written)
                {
                    FILE *f = fopen(temp_manifest.c_str(), "rb");
                    if (f)
                    {
                        std::string yaml;
                        char buf[4096];
                        std::size_t got;
                        while ((got = fread(buf, 1, sizeof(buf), f)) > 0)
                            yaml.append(buf, got);
                        fclose(f);

                        manifest_written =
                            mz_zip_writer_add_mem_ex(&zip, "Manifest.yml",
                                                     yaml.c_str(), yaml.size(),
                                                     NULL, 0,
                                                     MZ_DEFAULT_COMPRESSION,
                                                     0, 0) != 0;
                    }
                }

                fs_remove(temp_manifest);
            }

            if (!manifest_written)
            {
                mz_zip_writer_end(&zip);
                zip_close(&zip_io);
                fs_remove(temp_zip);
                result.error = "could not write the archive manifest";
                return result;
            }

            const bool finalised = mz_zip_writer_finalize_archive(&zip) != 0;

            mz_zip_writer_end(&zip);
            zip_close(&zip_io);

            if (!finalised)
            {
                fs_remove(temp_zip);
                result.error = "could not finalise the save archive";
                return result;
            }
        }

        lancommander::Result<bool> uploaded =
            ctx.saves->upload(ctx.game_id, temp_zip);

        fs_remove(temp_zip);

        if (!uploaded)
        {
            result.error = uploaded.error;
            return result;
        }

        result.ok = true;
        result.had_anything = true;

        log_info("Uploaded %d save file(s) for %s", result.entries,
                 ctx.title.c_str());

        return result;
    }

    // -----------------------------------------------------------------------
    // Download
    // -----------------------------------------------------------------------

    SaveSyncResult save_download(const SaveSyncContext &ctx)
    {
        SaveSyncResult result;

        if (!ctx.saves || ctx.game_id.empty() || ctx.install_dir.empty())
        {
            result.error = "nothing to download";
            result.ok = true;
            return result;
        }

        const std::string temp_zip = fs_temp_file("lcs");
        if (temp_zip.empty())
        {
            result.error = "could not create a temporary file";
            return result;
        }

        lancommander::Result<bool> downloaded =
            ctx.saves->download_latest(ctx.game_id, temp_zip);

        if (!downloaded)
        {
            fs_remove(temp_zip);

            // No save on the server is the normal state the first time anyone
            // plays a game. Not something to report as a failure.
            result.ok = true;
            result.had_anything = false;
            return result;
        }

        mz_zip_archive zip;
        ZipFile zip_io;

        if (!zip_open_read(&zip, &zip_io, temp_zip))
        {
            fs_remove(temp_zip);
            result.error = "the downloaded save archive could not be opened";
            return result;
        }

        // --- index the archive ---------------------------------------------
        std::vector<std::string> names;
        const mz_uint count = mz_zip_reader_get_num_files(&zip);

        for (mz_uint i = 0; i < count; ++i)
        {
            char name[512];
            const mz_uint got =
                mz_zip_reader_get_filename(&zip, i, name, sizeof(name));
            names.push_back(got ? to_archive_slashes(name) : std::string());
        }

        // Older archives use "Saves/" where current ones use "Files/".
        std::string prefix = "Files/";
        {
            bool has_files = false;
            for (std::size_t i = 0; i < names.size(); ++i)
            {
                if (starts_with(names[i], "Files/"))
                {
                    has_files = true;
                    break;
                }
            }
            if (!has_files)
                prefix = "Saves/";
        }

        // --- the archive's own manifest -------------------------------------
        GameManifest manifest;
        bool have_manifest = false;

        for (std::size_t i = 0; i < names.size(); ++i)
        {
            if (names[i] != "Manifest.yml")
                continue;

            const std::string temp_manifest = fs_temp_file("lcm");
            if (temp_manifest.empty())
                break;

            if (zip_extract_to_file(&zip, (unsigned int)i, temp_manifest))
            {
                lancommander::Result<std::string> json =
                    lancommander::manifest::read_json(temp_manifest);

                std::string parse_error;
                if (json && lancommander::parse_manifest_json(
                                json.value, &manifest, &parse_error))
                    have_manifest = true;
            }

            fs_remove(temp_manifest);
            break;
        }

        if (!have_manifest)
        {
            // Fall back to what is installed. The archive should carry one,
            // but a save is worth restoring even from an archive that does
            // not -- the installed manifest describes the same game.
            have_manifest =
                read_manifest_from_disk(ctx.install_dir, ctx.game_id, &manifest);
        }

        if (!have_manifest)
        {
            mz_zip_reader_end(&zip);
            zip_close(&zip_io);
            fs_remove(temp_zip);
            result.error = "the save archive has no manifest";
            return result;
        }

        // --- restore ---------------------------------------------------------
        for (std::size_t i = 0; i < manifest.save_paths.size(); ++i)
        {
            const ManifestSavePath &sp = manifest.save_paths[i];

            if (!sp.is_file)
                continue;
            if (!lancommander::script::supports_current_runtime(sp.platforms))
                continue;

            // The entries the archive recorded, when it has them.
            //
            // The .NET SDK recomputes these from the local filesystem instead,
            // which cannot restore a file that does not exist yet -- so a
            // regex save path restores nothing on a fresh install. The
            // archive already knows what it holds; use that, and only fall
            // back to recomputing for archives written before entries were
            // recorded.
            std::vector<SavePathEntry> entries = sp.entries;
            if (entries.empty())
                entries = save_path_entries(sp, ctx.install_dir);

            for (std::size_t e = 0; e < entries.size(); ++e)
            {
                const std::string base =
                    prefix + sp.id + "/" + entries[e].archive_path;
                const std::string destination =
                    expand_game_path(entries[e].actual_path, ctx.install_dir);

                for (std::size_t n = 0; n < names.size(); ++n)
                {
                    if (names[n].empty())
                        continue;

                    std::string target;

                    if (names[n] == base)
                    {
                        target = destination;
                    }
                    else if (starts_with(names[n], base + "/"))
                    {
                        // The entry named a directory.
                        target = destination + "/" +
                                 names[n].substr(base.size() + 1);
                    }
                    else
                    {
                        continue;
                    }

                    if (!target.empty() &&
                        (target[target.size() - 1] == '/' ||
                         target[target.size() - 1] == '\\'))
                        continue; // a directory entry in the zip; nothing to write

                    if (zip_extract_to_file(&zip, (unsigned int)n, target))
                        result.entries++;
                    else
                        log_warn("Could not restore %s", target.c_str());
                }
            }
        }

        mz_zip_reader_end(&zip);
        zip_close(&zip_io);
        fs_remove(temp_zip);

        result.ok = true;
        result.had_anything = result.entries > 0;

        log_info("Restored %d save file(s) for %s", result.entries,
                 ctx.title.c_str());

        // The SDK runs its SaveDownload script type only to shell out to
        // regedit for registry saves. A game-authored SaveDownload.ps1 is
        // still the documented hook for "the save just landed", so it runs
        // here -- after the files are in place, which is the only order that
        // makes it useful.
        run_save_script(ctx, lancommander::ScriptType::SaveDownload);

        return result;
    }

} // namespace launcher
