#include "json_helpers.h"

#include <cstdio>
#include <cstring>

namespace lancommander {
namespace json {

// ---------------------------------------------------------------------------
// Primitive accessors
// ---------------------------------------------------------------------------

// A JSON number or boolean where a string is expected is stringified rather
// than dropped, mirroring what a typed deserializer does.
//
// This matters most for YAML: a plain scalar carries no type, so a manifest
// written by YamlDotNet has `Version: 1.32` and `Value: 27960` that both read
// back as numbers even though the model stores them as strings.
static std::string stringify(cJSON* n)
{
    if (!n)
        return std::string();

    switch (n->type & 0xFF) {
        case cJSON_String:
            return n->valuestring ? std::string(n->valuestring) : std::string();

        case cJSON_True:
            return "true";

        case cJSON_False:
            return "false";

        case cJSON_Number: {
            char buffer[40];
            // Integers must not come back as "1.000000", and 1.32 must not come
            // back as "1.3200000000000001".
            if (n->valuedouble == (double)(long)n->valuedouble)
                std::sprintf(buffer, "%ld", (long)n->valuedouble);
            else
                std::sprintf(buffer, "%.15g", n->valuedouble);
            return std::string(buffer);
        }

        default:
            return std::string();
    }
}

std::string get_string(cJSON* obj, const char* camel, const char* pascal)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    return stringify(n);
}

std::string get_string(cJSON* obj, const char* key)
{
    return stringify(cJSON_GetObjectItem(obj, key));
}

int get_int(cJSON* obj, const char* camel, const char* pascal, int def)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    if (n && n->type == cJSON_Number) return n->valueint;
    return def;
}

int get_int(cJSON* obj, const char* key, int def)
{
    cJSON* n = cJSON_GetObjectItem(obj, key);
    if (n && n->type == cJSON_Number) return n->valueint;
    return def;
}

long long get_long(cJSON* obj, const char* camel, const char* pascal, long long def)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    if (n && n->type == cJSON_Number) return static_cast<long long>(n->valuedouble);
    return def;
}

bool get_bool(cJSON* obj, const char* camel, const char* pascal, bool def)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    if (!n) return def;
    if (n->type == cJSON_True)  return true;
    if (n->type == cJSON_False) return false;
    if (n->type == cJSON_Number) return n->valueint != 0;
    return def;
}

bool get_bool(cJSON* obj, const char* key, bool def)
{
    cJSON* n = cJSON_GetObjectItem(obj, key);
    if (!n) return def;
    if (n->type == cJSON_True)  return true;
    if (n->type == cJSON_False) return false;
    if (n->type == cJSON_Number) return n->valueint != 0;
    return def;
}

cJSON* get_child(cJSON* obj, const char* camel, const char* pascal)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    return n;
}

std::vector<std::string> collect_names(cJSON* arr)
{
    std::vector<std::string> result;
    if (!arr || arr->type != cJSON_Array) return result;
    int n = cJSON_GetArraySize(arr);
    for (int i = 0; i < n; ++i) {
        cJSON* e = cJSON_GetArrayItem(arr, i);
        if (!e) continue;
        std::string name = get_string(e, "name", "Name");
        if (!name.empty()) result.push_back(name);
    }
    return result;
}

std::vector<std::string> collect_strings(cJSON* arr)
{
    std::vector<std::string> result;
    if (!arr || arr->type != cJSON_Array) return result;
    int n = cJSON_GetArraySize(arr);
    for (int i = 0; i < n; ++i) {
        cJSON* e = cJSON_GetArrayItem(arr, i);
        if (e && e->type == cJSON_String && e->valuestring)
            result.push_back(std::string(e->valuestring));
    }
    return result;
}

std::string escape(const std::string& in)
{
    std::string out;
    out.reserve(in.size() + 2);
    for (size_t i = 0; i < in.size(); ++i) {
        char c = in[i];
        switch (c) {
            case '"':  out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\b': out += "\\b";  break;
            case '\f': out += "\\f";  break;
            case '\n': out += "\\n";  break;
            case '\r': out += "\\r";  break;
            case '\t': out += "\\t";  break;
            default:   out += c;      break;
        }
    }
    return out;
}

// ---------------------------------------------------------------------------
// Model parsers — existing
// ---------------------------------------------------------------------------

AuthToken parse_auth_token(cJSON* obj)
{
    AuthToken t;
    t.access_token  = get_string(obj, "accessToken",  "AccessToken");
    t.refresh_token = get_string(obj, "refreshToken", "RefreshToken");
    t.expiration    = get_string(obj, "expiration",    "Expiration");
    return t;
}

MediaRef parse_media_ref(cJSON* obj)
{
    MediaRef r;
    r.id      = get_string(obj, "id", "Id");
    r.crc32   = get_string(obj, "crc32", "Crc32");
    r.file_id = get_string(obj, "fileId", "FileId");

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_String && t->valuestring) {
        r.type = t->valuestring;
    } else if (t && t->type == cJSON_Number) {
        static const char* kNames[] = {
            "Icon", "Cover", "Background", "Avatar", "Logo",
            "Manual", "Thumbnail", "PageImage", "Grid",
            "Screenshot", "Video"
        };
        int v = t->valueint;
        if (v >= 0 && v < static_cast<int>(sizeof(kNames) / sizeof(kNames[0])))
            r.type = kNames[v];
    }
    return r;
}

Game parse_game(cJSON* obj)
{
    Game g;
    g.id          = get_string(obj, "id", "Id");
    g.title       = get_string(obj, "title", "Title");
    g.sort_title  = get_string(obj, "sortTitle", "SortTitle");
    g.description = get_string(obj, "description", "Description");
    g.notes       = get_string(obj, "notes", "Notes");
    g.in_library  = get_bool(obj, "inLibrary", "InLibrary", false);
    g.base_game_id = get_string(obj, "baseGameId", "BaseGameId");

    // Parse released year from date string
    std::string released = get_string(obj, "releasedOn", "ReleasedOn");
    if (released.size() >= 4) {
        int year = 0;
        for (int k = 0; k < 4 && released[k] >= '0' && released[k] <= '9'; ++k)
            year = year * 10 + (released[k] - '0');
        if (year >= 1970) g.released_year = year;
    }

    // Game type
    cJSON* gt = get_child(obj, "type", "Type");
    if (gt && gt->type == cJSON_Number)
        g.type = static_cast<GameType>(gt->valueint);

    // Related names
    g.developers = collect_names(get_child(obj, "developers", "Developers"));
    g.publishers = collect_names(get_child(obj, "publishers", "Publishers"));
    g.genres     = collect_names(get_child(obj, "genres", "Genres"));

    // Media array
    cJSON* media = get_child(obj, "media", "Media");
    if (media && media->type == cJSON_Array) {
        int mc = cJSON_GetArraySize(media);
        for (int i = 0; i < mc; ++i) {
            cJSON* m = cJSON_GetArrayItem(media, i);
            if (!m) continue;
            MediaRef ref = parse_media_ref(m);
            if (ref.id.empty()) continue;

            // Track cover media
            if (g.cover_media_id.empty() && (ref.type == "Cover" || ref.type == "1"))  {
                g.cover_media_id = ref.id;
                g.cover_crc32    = ref.crc32;
            }
            g.media.push_back(std::move(ref));
        }

        // Fallback: if no Cover found, use the first media
        if (g.cover_media_id.empty() && !g.media.empty()) {
            g.cover_media_id = g.media[0].id;
            g.cover_crc32    = g.media[0].crc32;
        }
    }

    cJSON* archives = get_child(obj, "archives", "Archives");
    if (archives && archives->type == cJSON_Array) {
        int ac = cJSON_GetArraySize(archives);
        for (int i = 0; i < ac; ++i) {
            cJSON* a = cJSON_GetArrayItem(archives, i);
            if (!a) continue;
            g.archives.push_back(parse_archive(a));
        }
    }

    return g;
}

ManifestAction parse_manifest_action(cJSON* obj)
{
    ManifestAction a;
    a.name              = get_string(obj, "name", "Name");
    a.path              = get_string(obj, "path", "Path");
    a.arguments         = get_string(obj, "arguments", "Arguments");
    a.working_directory = get_string(obj, "workingDirectory", "WorkingDirectory");
    a.is_primary        = get_bool(obj, "isPrimaryAction", "IsPrimaryAction", false);
    a.sort_order        = get_int(obj, "sortOrder", "SortOrder", 0);

    cJSON* vars = get_child(obj, "variables", "Variables");
    if (vars && vars->type == cJSON_Object) {
        for (cJSON* v = vars->child; v; v = v->next) {
            if (v->string && v->type == cJSON_String && v->valuestring)
                a.variables[v->string] = v->valuestring;
        }
    }
    return a;
}

ManifestSavePath parse_manifest_save_path(cJSON* obj)
{
    ManifestSavePath sp;
    sp.id                = get_string(obj, "id", "Id");
    sp.path              = get_string(obj, "path", "Path");
    sp.working_directory = get_string(obj, "workingDirectory", "WorkingDirectory");
    sp.is_regex          = get_bool(obj, "isRegex", "IsRegex", false);

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_String && t->valuestring)
        sp.is_file = (strcmp(t->valuestring, "File") == 0);
    else if (t && t->type == cJSON_Number)
        sp.is_file = (t->valueint == 0);
    else
        sp.is_file = true;

    sp.platforms = parse_runtime_platform(get_child(obj, "platforms", "Platforms"));

    cJSON* entries = get_child(obj, "entries", "Entries");
    if (entries && entries->type == cJSON_Array) {
        const int n = cJSON_GetArraySize(entries);
        for (int i = 0; i < n; ++i) {
            cJSON* e = cJSON_GetArrayItem(entries, i);
            if (!e)
                continue;

            SavePathEntry entry;
            entry.archive_path = get_string(e, "archivePath", "ArchivePath");
            entry.actual_path = get_string(e, "actualPath", "ActualPath");
            sp.entries.push_back(entry);
        }
    }

    return sp;
}

Redistributable parse_redistributable(cJSON* obj)
{
    Redistributable r;
    r.id          = get_string(obj, "id", "Id");
    r.name        = get_string(obj, "name", "Name");
    r.description = get_string(obj, "description", "Description");

    cJSON* scripts = get_child(obj, "scripts", "Scripts");
    if (scripts && scripts->type == cJSON_Array) {
        const int n = cJSON_GetArraySize(scripts);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(scripts, i);
            if (item) r.scripts.push_back(parse_script(item));
        }
    }

    return r;
}

ScriptType script_type_from_ordinal(int ordinal)
{
    switch (ordinal) {
        case 0:  return ScriptType::Install;
        case 1:  return ScriptType::Uninstall;
        case 2:  return ScriptType::NameChange;
        case 3:  return ScriptType::KeyChange;
        case 4:  return ScriptType::SaveUpload;
        case 5:  return ScriptType::SaveDownload;
        case 6:  return ScriptType::DetectInstall;
        case 7:  return ScriptType::BeforeStart;
        case 8:  return ScriptType::AfterStop;
        case 9:  return ScriptType::GameStarted;
        case 10: return ScriptType::GameStopped;
        case 11: return ScriptType::UserRegistration;
        case 12: return ScriptType::UserLogin;
        case 13: return ScriptType::ApplicationStart;
        case 14: return ScriptType::Package;
        case 15: return ScriptType::RunWrapper;
        default: return ScriptType::Unknown;
    }
}

ScriptType script_type_from_name(const std::string& name)
{
    if (name == "Install")               return ScriptType::Install;
    if (name == "Uninstall")             return ScriptType::Uninstall;
    if (name == "NameChange")            return ScriptType::NameChange;
    if (name == "KeyChange")             return ScriptType::KeyChange;
    if (name == "SaveUpload")            return ScriptType::SaveUpload;
    if (name == "SaveDownload")          return ScriptType::SaveDownload;
    if (name == "DetectInstall")         return ScriptType::DetectInstall;
    if (name == "BeforeStart")           return ScriptType::BeforeStart;
    if (name == "AfterStop")             return ScriptType::AfterStop;
    if (name == "GameStarted")           return ScriptType::GameStarted;
    if (name == "GameStopped")           return ScriptType::GameStopped;
    if (name == "UserRegistration")      return ScriptType::UserRegistration;
    if (name == "UserLogin")             return ScriptType::UserLogin;
    if (name == "ApplicationStart")      return ScriptType::ApplicationStart;
    if (name == "Package")               return ScriptType::Package;
    if (name == "RunWrapper")            return ScriptType::RunWrapper;
    return ScriptType::Unknown;
}

// Accepts the three shapes a [Flags] enum can take on the wire: an ordinal, a
// comma-separated name list ("Windows, Linux"), or an array of names.
int parse_runtime_platform(cJSON* value)
{
    if (!value)
        return RuntimePlatform_None;

    if (value->type == cJSON_Number)
        return value->valueint;

    if (value->type == cJSON_String && value->valuestring) {
        int flags = RuntimePlatform_None;
        const std::string text = value->valuestring;
        std::string token;

        for (std::size_t i = 0; i <= text.size(); ++i) {
            const bool at_end = (i == text.size());
            if (!at_end && text[i] != ',') {
                if (text[i] != ' ')
                    token += text[i];
                continue;
            }
            if (token == "Windows")    flags |= RuntimePlatform_Windows;
            else if (token == "Linux") flags |= RuntimePlatform_Linux;
            else if (token == "macOS") flags |= RuntimePlatform_macOS;
            token.clear();
        }
        return flags;
    }

    if (value->type == cJSON_Array) {
        int flags = RuntimePlatform_None;
        const int n = cJSON_GetArraySize(value);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(value, i);
            if (item)
                flags |= parse_runtime_platform(item);
        }
        return flags;
    }

    return RuntimePlatform_None;
}

Script parse_script(cJSON* obj)
{
    Script s;
    s.name        = get_string(obj, "name", "Name");
    s.description = get_string(obj, "description", "Description");
    s.contents    = get_string(obj, "contents", "Contents");

    s.requires_admin = get_bool(obj, "requiresAdmin", "RequiresAdmin", false);
    s.platforms      = parse_runtime_platform(
        get_child(obj, "platforms", "Platforms"));

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_Number)
        s.type = script_type_from_ordinal(t->valueint);
    else if (t && t->type == cJSON_String && t->valuestring)
        s.type = script_type_from_name(t->valuestring);
    else
        s.type = ScriptType::Unknown;

    return s;
}

EntityReference parse_entity_reference(cJSON* obj)
{
    EntityReference e;
    e.id   = get_string(obj, "id", "Id");
    e.name = get_string(obj, "name", "Name");
    return e;
}

User parse_user(cJSON* obj)
{
    User u;
    u.id        = get_string(obj, "id", "Id");
    u.user_name = get_string(obj, "userName", "UserName");
    u.alias     = get_string(obj, "alias", "Alias");
    return u;
}

GameSave parse_game_save(cJSON* obj)
{
    GameSave s;
    s.id         = get_string(obj, "id", "Id");
    s.game_id    = get_string(obj, "gameId", "GameId");
    s.created_on = get_string(obj, "createdOn", "CreatedOn");
    s.updated_on = get_string(obj, "updatedOn", "UpdatedOn");
    return s;
}

DiscoveredServer parse_discovered_server(cJSON* obj)
{
    DiscoveredServer s;
    s.address = get_string(obj, "address", "Address");
    s.name    = get_string(obj, "name", "Name");
    s.version = get_string(obj, "version", "Version");
    return s;
}

// ---------------------------------------------------------------------------
// Model parsers — new
// ---------------------------------------------------------------------------

Archive parse_archive(cJSON* obj)
{
    Archive a;
    a.id                = get_string(obj, "id", "Id");
    a.changelog         = get_string(obj, "changelog", "Changelog");
    a.object_key        = get_string(obj, "objectKey", "ObjectKey");
    a.version           = get_string(obj, "version", "Version");
    a.compressed_size   = get_long(obj, "compressedSize", "CompressedSize", 0);
    a.uncompressed_size = get_long(obj, "uncompressedSize", "UncompressedSize", 0);
    a.created_on        = get_string(obj, "createdOn", "CreatedOn");
    a.updated_on        = get_string(obj, "updatedOn", "UpdatedOn");
    return a;
}

Tool parse_tool(cJSON* obj)
{
    Tool t;
    t.id          = get_string(obj, "id", "Id");
    t.name        = get_string(obj, "name", "Name");
    t.description = get_string(obj, "description", "Description");
    t.notes       = get_string(obj, "notes", "Notes");
    t.released_on = get_string(obj, "releasedOn", "ReleasedOn");
    t.created_on  = get_string(obj, "createdOn", "CreatedOn");
    t.updated_on  = get_string(obj, "updatedOn", "UpdatedOn");

    cJSON* archives = get_child(obj, "archives", "Archives");
    if (archives && archives->type == cJSON_Array) {
        int n = cJSON_GetArraySize(archives);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(archives, i);
            if (item) t.archives.push_back(parse_archive(item));
        }
    }

    cJSON* scripts = get_child(obj, "scripts", "Scripts");
    if (scripts && scripts->type == cJSON_Array) {
        int n = cJSON_GetArraySize(scripts);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(scripts, i);
            if (item) t.scripts.push_back(parse_script(item));
        }
    }

    return t;
}

Collection parse_collection(cJSON* obj)
{
    Collection c;
    c.id   = get_string(obj, "id", "Id");
    c.name = get_string(obj, "name", "Name");
    return c;
}

Company parse_company(cJSON* obj)
{
    Company c;
    c.id   = get_string(obj, "id", "Id");
    c.name = get_string(obj, "name", "Name");
    return c;
}

Engine parse_engine(cJSON* obj)
{
    Engine e;
    e.id   = get_string(obj, "id", "Id");
    e.name = get_string(obj, "name", "Name");
    return e;
}

Genre parse_genre(cJSON* obj)
{
    Genre g;
    g.id   = get_string(obj, "id", "Id");
    g.name = get_string(obj, "name", "Name");
    return g;
}

Platform parse_platform(cJSON* obj)
{
    Platform p;
    p.id   = get_string(obj, "id", "Id");
    p.name = get_string(obj, "name", "Name");
    return p;
}

Tag parse_tag(cJSON* obj)
{
    Tag t;
    t.id   = get_string(obj, "id", "Id");
    t.name = get_string(obj, "name", "Name");
    return t;
}

PlaySession parse_play_session(cJSON* obj)
{
    PlaySession ps;
    ps.id         = get_string(obj, "id", "Id");
    ps.start      = get_string(obj, "start", "Start");
    ps.end        = get_string(obj, "end", "End");
    ps.game_id    = get_string(obj, "gameId", "GameId");
    ps.user_id    = get_string(obj, "userId", "UserId");
    ps.created_on = get_string(obj, "createdOn", "CreatedOn");
    ps.updated_on = get_string(obj, "updatedOn", "UpdatedOn");
    return ps;
}

GameCustomField parse_custom_field(cJSON* obj)
{
    GameCustomField f;
    f.name  = get_string(obj, "name", "Name");
    f.value = get_string(obj, "value", "Value");
    return f;
}

GameExternalId parse_external_id(cJSON* obj)
{
    GameExternalId e;
    e.id          = get_string(obj, "id", "Id");
    e.provider    = get_string(obj, "provider", "Provider");
    e.external_id = get_string(obj, "externalId", "ExternalId");
    e.created_on  = get_string(obj, "createdOn", "CreatedOn");
    e.updated_on  = get_string(obj, "updatedOn", "UpdatedOn");
    return e;
}

MultiplayerMode parse_multiplayer_mode(cJSON* obj)
{
    MultiplayerMode m;
    m.id          = get_string(obj, "id", "Id");
    m.description = get_string(obj, "description", "Description");
    m.min_players = get_int(obj, "minPlayers", "MinPlayers", 0);
    m.max_players = get_int(obj, "maxPlayers", "MaxPlayers", 0);
    m.spectators  = get_int(obj, "spectators", "Spectators", 0);

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_Number)
        m.type = static_cast<MultiplayerType>(t->valueint);

    cJSON* np = get_child(obj, "networkProtocol", "NetworkProtocol");
    if (np && np->type == cJSON_Number)
        m.network_protocol = static_cast<NetworkProtocol>(np->valueint);

    return m;
}

Lobby parse_lobby(cJSON* obj)
{
    Lobby l;
    l.id                = get_string(obj, "id", "Id");
    l.game_id           = get_string(obj, "gameId", "GameId");
    l.external_game_id  = get_string(obj, "externalGameId", "ExternalGameId");
    l.external_username = get_string(obj, "externalUsername", "ExternalUsername");
    l.external_user_id  = get_string(obj, "externalUserId", "ExternalUserId");
    return l;
}

Page parse_page(cJSON* obj)
{
    Page p;
    p.id         = get_string(obj, "id", "Id");
    p.title      = get_string(obj, "title", "Title");
    p.slug       = get_string(obj, "slug", "Slug");
    p.route      = get_string(obj, "route", "Route");
    p.contents   = get_string(obj, "contents", "Contents");
    p.sort_order = get_int(obj, "sortOrder", "SortOrder", 0);
    p.parent_id  = get_string(obj, "parentId", "ParentId");
    p.created_on = get_string(obj, "createdOn", "CreatedOn");
    p.updated_on = get_string(obj, "updatedOn", "UpdatedOn");
    return p;
}

Package parse_package(cJSON* obj)
{
    Package p;
    p.path      = get_string(obj, "path", "Path");
    p.version   = get_string(obj, "version", "Version");
    p.changelog = get_string(obj, "changelog", "Changelog");
    return p;
}

Media parse_media(cJSON* obj)
{
    Media m;
    m.id         = get_string(obj, "id", "Id");
    m.file_id    = get_string(obj, "fileId", "FileId");
    m.crc32      = get_string(obj, "crc32", "Crc32");
    m.source_url = get_string(obj, "sourceUrl", "SourceUrl");

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_Number)
        m.type = static_cast<MediaType>(t->valueint);
    else if (t && t->type == cJSON_String && t->valuestring) {
        std::string sv = t->valuestring;
        if (sv == "Icon")            m.type = MediaType::Icon;
        else if (sv == "Cover")      m.type = MediaType::Cover;
        else if (sv == "Background") m.type = MediaType::Background;
        else if (sv == "Avatar")     m.type = MediaType::Avatar;
        else if (sv == "Logo")       m.type = MediaType::Logo;
        else if (sv == "Manual")     m.type = MediaType::Manual;
        else if (sv == "PageImage")  m.type = MediaType::PageImage;
        else if (sv == "Grid")       m.type = MediaType::Grid;
        else if (sv == "Screenshot") m.type = MediaType::Screenshot;
        else if (sv == "Video")      m.type = MediaType::Video;
    }

    return m;
}

DepotGame parse_depot_game(cJSON* obj)
{
    DepotGame dg;
    dg.id             = get_string(obj, "id", "Id");
    dg.title          = get_string(obj, "title", "Title");
    dg.sort_title     = get_string(obj, "sortTitle", "SortTitle");
    dg.directory_name = get_string(obj, "directoryName", "DirectoryName");
    dg.notes          = get_string(obj, "notes", "Notes");
    dg.description    = get_string(obj, "description", "Description");
    dg.singleplayer   = get_bool(obj, "singleplayer", "Singleplayer", false);
    dg.created_on     = get_string(obj, "createdOn", "CreatedOn");
    dg.released_on    = get_string(obj, "releasedOn", "ReleasedOn");
    dg.in_library     = get_bool(obj, "inLibrary", "InLibrary", false);
    dg.engine_id      = get_string(obj, "engineId", "EngineId");

    cJSON* gt = get_child(obj, "type", "Type");
    if (gt && gt->type == cJSON_Number)
        dg.type = static_cast<GameType>(gt->valueint);

    cJSON* cover = get_child(obj, "cover", "Cover");
    if (cover && cover->type == cJSON_Object)
        dg.cover = parse_media(cover);

    cJSON* arr;

    arr = get_child(obj, "collections", "Collections");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.collections.push_back(parse_collection(item));
        }
    }

    arr = get_child(obj, "developers", "Developers");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.developers.push_back(parse_company(item));
        }
    }

    arr = get_child(obj, "publishers", "Publishers");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.publishers.push_back(parse_company(item));
        }
    }

    arr = get_child(obj, "genres", "Genres");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.genres.push_back(parse_genre(item));
        }
    }

    arr = get_child(obj, "multiplayerModes", "MultiplayerModes");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.multiplayer_modes.push_back(parse_multiplayer_mode(item));
        }
    }

    arr = get_child(obj, "platforms", "Platforms");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.platforms.push_back(parse_platform(item));
        }
    }

    arr = get_child(obj, "tags", "Tags");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dg.tags.push_back(parse_tag(item));
        }
    }

    return dg;
}

DepotResults parse_depot_results(cJSON* obj)
{
    DepotResults dr;
    cJSON* arr;

    arr = get_child(obj, "games", "Games");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.games.push_back(parse_depot_game(item));
        }
    }

    arr = get_child(obj, "collections", "Collections");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.collections.push_back(parse_collection(item));
        }
    }

    arr = get_child(obj, "companies", "Companies");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.companies.push_back(parse_company(item));
        }
    }

    arr = get_child(obj, "engines", "Engines");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.engines.push_back(parse_engine(item));
        }
    }

    arr = get_child(obj, "genres", "Genres");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.genres.push_back(parse_genre(item));
        }
    }

    arr = get_child(obj, "platforms", "Platforms");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.platforms.push_back(parse_platform(item));
        }
    }

    arr = get_child(obj, "tags", "Tags");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) dr.tags.push_back(parse_tag(item));
        }
    }

    arr = get_child(obj, "popular", "Popular");
    dr.popular = collect_strings(arr);

    arr = get_child(obj, "backlog", "Backlog");
    dr.backlog = collect_strings(arr);

    return dr;
}

CheckForUpdateResponse parse_check_for_update_response(cJSON* obj)
{
    CheckForUpdateResponse r;
    r.update_available = get_bool(obj, "updateAvailable", "UpdateAvailable", false);
    r.version          = get_string(obj, "version", "Version");
    r.download_url     = get_string(obj, "downloadUrl", "DownloadUrl");
    return r;
}

ErrorInfo parse_error_info(cJSON* obj)
{
    ErrorInfo ei;
    ei.key     = get_string(obj, "key", "Key");
    ei.message = get_string(obj, "message", "Message");
    return ei;
}

ErrorResponse parse_error_response(cJSON* obj)
{
    ErrorResponse er;
    er.error   = get_string(obj, "error", "Error");
    er.message = get_string(obj, "message", "Message");

    cJSON* details = get_child(obj, "details", "Details");
    if (details && details->type == cJSON_Array) {
        int n = cJSON_GetArraySize(details);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(details, i);
            if (item) er.details.push_back(parse_error_info(item));
        }
    }

    return er;
}

ServerConsole parse_server_console(cJSON* obj)
{
    ServerConsole sc;
    sc.id   = get_string(obj, "id", "Id");
    sc.name = get_string(obj, "name", "Name");

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_Number)
        sc.type = static_cast<ServerConsoleType>(t->valueint);

    return sc;
}

ServerHttpPath parse_server_http_path(cJSON* obj)
{
    ServerHttpPath hp;
    hp.id   = get_string(obj, "id", "Id");
    hp.path = get_string(obj, "path", "Path");
    return hp;
}

ServerDetail parse_server_detail(cJSON* obj)
{
    ServerDetail s;
    s.id                = get_string(obj, "id", "Id");
    s.name              = get_string(obj, "name", "Name");
    s.path              = get_string(obj, "path", "Path");
    s.arguments         = get_string(obj, "arguments", "Arguments");
    s.working_directory = get_string(obj, "workingDirectory", "WorkingDirectory");
    s.host              = get_string(obj, "host", "Host");
    s.port              = get_int(obj, "port", "Port", 0);
    s.use_shell_execute = get_bool(obj, "useShellExecute", "UseShellExecute", false);
    s.autostart         = get_bool(obj, "autostart", "Autostart", false);
    s.autostart_delay   = get_int(obj, "autostartDelay", "AutostartDelay", 0);
    s.game_id           = get_string(obj, "gameId", "GameId");
    s.created_on        = get_string(obj, "createdOn", "CreatedOn");
    s.updated_on        = get_string(obj, "updatedOn", "UpdatedOn");

    cJSON* ptm = get_child(obj, "processTerminationMethod", "ProcessTerminationMethod");
    if (ptm && ptm->type == cJSON_Number)
        s.process_termination_method = static_cast<ProcessTerminationMethod>(ptm->valueint);

    cJSON* asm_ = get_child(obj, "autostartMethod", "AutostartMethod");
    if (asm_ && asm_->type == cJSON_Number)
        s.autostart_method = static_cast<ServerAutostartMethod>(asm_->valueint);

    cJSON* arr;

    arr = get_child(obj, "serverConsoles", "ServerConsoles");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) s.server_consoles.push_back(parse_server_console(item));
        }
    }

    arr = get_child(obj, "httpPaths", "HttpPaths");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) s.http_paths.push_back(parse_server_http_path(item));
        }
    }

    arr = get_child(obj, "scripts", "Scripts");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (item) s.scripts.push_back(parse_script(item));
        }
    }

    return s;
}

} // namespace json
} // namespace lancommander
