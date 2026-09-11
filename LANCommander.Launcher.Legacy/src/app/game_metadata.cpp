#include "app/game_metadata.h"

#include <lancommander/script/script_helper.h>
#include <lancommander/util/path.h>

#include <cstdio>
#include <cstdlib>
#include <cstring>

#ifdef _WIN32
#include <windows.h>
#endif

namespace launcher
{

    namespace
    {
        std::string metadata_file(const std::string &install_dir,
                                  const std::string &game_id, const char *name)
        {
            return lancommander::script::metadata_directory_path(install_dir,
                                                                 game_id) +
                   "/" + name;
        }

        std::string read_text_file(const std::string &path)
        {
            FILE *f = fopen(path.c_str(), "rb");
            if (!f)
                return std::string();

            std::string text;
            char buf[256];
            std::size_t got;
            while ((got = fread(buf, 1, sizeof(buf), f)) > 0)
                text.append(buf, got);
            fclose(f);

            // The .NET side writes the bare string, but a file edited by hand
            // arrives with a newline on it and would then never compare equal
            // to what the server sent.
            while (!text.empty() &&
                   (text[text.size() - 1] == '\n' || text[text.size() - 1] == '\r'))
                text.erase(text.size() - 1);

            return text;
        }

        bool write_text_file(const std::string &install_dir,
                             const std::string &game_id, const char *name,
                             const std::string &text)
        {
            if (install_dir.empty() || game_id.empty())
                return false;

            lancommander::path::create_directories(
                lancommander::script::metadata_directory_path(install_dir, game_id));

            FILE *f = fopen(metadata_file(install_dir, game_id, name).c_str(), "wb");
            if (!f)
                return false;

            if (!text.empty())
                fwrite(text.c_str(), 1, text.size(), f);
            fclose(f);

            return true;
        }

        char native_separator()
        {
#ifdef _WIN32
            return '\\';
#else
            return '/';
#endif
        }

        void normalise_separators(std::string &s)
        {
            const char sep = native_separator();
            for (std::size_t i = 0; i < s.size(); ++i)
            {
                if (s[i] == '/' || s[i] == '\\')
                    s[i] = sep;
            }
        }

        void trim_trailing_separator(std::string &s)
        {
            while (s.size() > 1 &&
                   (s[s.size() - 1] == '/' || s[s.size() - 1] == '\\'))
                s.erase(s.size() - 1);
        }

        void replace_all(std::string &s, const std::string &from,
                         const std::string &to)
        {
            if (from.empty())
                return;

            std::string::size_type at = 0;
            while ((at = s.find(from, at)) != std::string::npos)
            {
                s.replace(at, from.size(), to);
                at += to.size();
            }
        }

        bool equal_nocase(const std::string &a, const std::string &b)
        {
            if (a.size() != b.size())
                return false;
            for (std::size_t i = 0; i < a.size(); ++i)
            {
                char ca = a[i];
                char cb = b[i];
                if (ca >= 'a' && ca <= 'z') ca = (char)(ca - 'a' + 'A');
                if (cb >= 'a' && cb <= 'z') cb = (char)(cb - 'a' + 'A');
                if (ca != cb)
                    return false;
            }
            return true;
        }

        // Case-insensitive find, because Windows paths are and a save path
        // written as %APPDATA% must match a real path spelled %appdata%.
        std::string::size_type find_nocase(const std::string &hay,
                                           const std::string &needle)
        {
            if (needle.empty() || needle.size() > hay.size())
                return std::string::npos;

            for (std::size_t i = 0; i + needle.size() <= hay.size(); ++i)
            {
                if (equal_nocase(hay.substr(i, needle.size()), needle))
                    return i;
            }
            return std::string::npos;
        }

        // %VAR% expansion. Done here rather than with ExpandEnvironmentStrings
        // so DOS and Win9x behave the same as everything else, and so an
        // unset variable is left alone rather than blanked -- blanking turns
        // "%APPDATA%\Game" into "\Game", which is a real directory and the
        // wrong one.
        std::string expand_environment(const std::string &path)
        {
            std::string out;

            for (std::size_t i = 0; i < path.size(); ++i)
            {
                if (path[i] != '%')
                {
                    out += path[i];
                    continue;
                }

                const std::string::size_type close = path.find('%', i + 1);
                if (close == std::string::npos)
                {
                    out += path[i];
                    continue;
                }

                const std::string name = path.substr(i + 1, close - i - 1);
                const char *value = name.empty() ? NULL : getenv(name.c_str());

                if (value)
                {
                    out += value;
                    i = close;
                }
                else
                {
                    out += path[i];
                }
            }

            return out;
        }

        // The special folders the .NET SDK deflates paths against, in the order
        // it tries them. Longest-match-first is what makes this correct: a save
        // under Documents must not deflate against the profile root.
        struct SpecialFolder
        {
            const char *token; // what appears in a manifest
            const char *env;   // the environment variable behind it
            const char *tail;  // appended to the variable, or NULL
        };

        const SpecialFolder *special_folders(int *count)
        {
            static const SpecialFolder folders[] = {
                { "%APPDATA%",           "APPDATA",        NULL },
                { "%LOCALAPPDATA%",      "LOCALAPPDATA",   NULL },
                { "%PROGRAMDATA%",       "ProgramData",    NULL },
                { "%ALLUSERSPROFILE%",   "ALLUSERSPROFILE", NULL },
                { "%USERPROFILE%\\Documents", "USERPROFILE", "\\Documents" },
                { "%USERPROFILE%\\Desktop",   "USERPROFILE", "\\Desktop" },
                { "%USERPROFILE%\\Music",     "USERPROFILE", "\\Music" },
                { "%USERPROFILE%\\Pictures",  "USERPROFILE", "\\Pictures" },
                { "%USERPROFILE%\\Videos",    "USERPROFILE", "\\Videos" },
                { "%USERPROFILE%",       "USERPROFILE",    NULL },
                { "%PUBLIC%",            "PUBLIC",         NULL }
            };

            *count = (int)(sizeof(folders) / sizeof(folders[0]));
            return folders;
        }

        std::string special_folder_value(const SpecialFolder &folder)
        {
            const char *base = getenv(folder.env);
            if (!base || !*base)
                return std::string();

            std::string value = base;
            if (folder.tail)
                value += folder.tail;

            trim_trailing_separator(value);

            return value;
        }
    } // namespace

    // -----------------------------------------------------------------------
    // Player alias
    // -----------------------------------------------------------------------

    std::string game_player_alias(const std::string &install_dir,
                                  const std::string &game_id)
    {
        if (install_dir.empty() || game_id.empty())
            return std::string();

        return read_text_file(metadata_file(install_dir, game_id, "PlayerAlias"));
    }

    bool set_game_player_alias(const std::string &install_dir,
                               const std::string &game_id,
                               const std::string &alias)
    {
        return write_text_file(install_dir, game_id, "PlayerAlias", alias);
    }

    // -----------------------------------------------------------------------
    // Key
    // -----------------------------------------------------------------------

    std::string game_key(const std::string &install_dir,
                         const std::string &game_id)
    {
        if (install_dir.empty() || game_id.empty())
            return std::string();

        return read_text_file(metadata_file(install_dir, game_id, "Key"));
    }

    bool set_game_key(const std::string &install_dir, const std::string &game_id,
                      const std::string &key)
    {
        return write_text_file(install_dir, game_id, "Key", key);
    }

    // -----------------------------------------------------------------------
    // Path tokens
    // -----------------------------------------------------------------------

    std::string expand_game_path(const std::string &path,
                                 const std::string &install_dir)
    {
        if (path.empty())
            return path;

        std::string out = path;

        std::string root = install_dir;
        trim_trailing_separator(root);
        replace_all(out, "{InstallDir}", root);

        out = expand_environment(out);

        normalise_separators(out);
        trim_trailing_separator(out);

        return out;
    }

    std::string deflate_game_path(const std::string &path,
                                  const std::string &install_dir)
    {
        if (path.empty())
            return path;

        std::string out = path;
        normalise_separators(out);

        std::string root = install_dir;
        normalise_separators(root);
        trim_trailing_separator(root);

        // The install directory first: a game that keeps its saves beside
        // itself must deflate to {InstallDir}, not to whatever special folder
        // it happens to sit under.
        if (!root.empty())
        {
            const std::string::size_type at = find_nocase(out, root);
            if (at == 0)
                out = "{InstallDir}" + out.substr(root.size());
        }

        int count = 0;
        const SpecialFolder *folders = special_folders(&count);

        for (int i = 0; i < count; ++i)
        {
            std::string value = special_folder_value(folders[i]);
            if (value.empty())
                continue;

            normalise_separators(value);

            const std::string::size_type at = find_nocase(out, value);
            if (at != 0)
                continue;

            // Only a whole path component, so %PUBLIC% does not swallow the
            // front of "C:\PublicWorks".
            const std::size_t after = value.size();
            if (after < out.size() && out[after] != '/' && out[after] != '\\')
                continue;

            out = std::string(folders[i].token) + out.substr(after);
            break; // the table is ordered longest-first; the first hit wins
        }

        // Archive paths are always forward-slashed, so an archive packed on
        // Windows restores on Linux and the other way round.
        for (std::size_t i = 0; i < out.size(); ++i)
        {
            if (out[i] == '\\')
                out[i] = '/';
        }

        return out;
    }

} // namespace launcher
