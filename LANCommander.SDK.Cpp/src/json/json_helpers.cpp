#include "json_helpers.h"

#include <cstring>

namespace lancommander {
namespace json {

// ---------------------------------------------------------------------------
// Primitive accessors
// ---------------------------------------------------------------------------

std::string get_string(cJSON* obj, const char* camel, const char* pascal)
{
    cJSON* n = cJSON_GetObjectItem(obj, camel);
    if (!n) n = cJSON_GetObjectItem(obj, pascal);
    if (n && n->type == cJSON_String && n->valuestring)
        return std::string(n->valuestring);
    return {};
}

std::string get_string(cJSON* obj, const char* key)
{
    cJSON* n = cJSON_GetObjectItem(obj, key);
    if (n && n->type == cJSON_String && n->valuestring)
        return std::string(n->valuestring);
    return {};
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

    return sp;
}

Redistributable parse_redistributable(cJSON* obj)
{
    Redistributable r;
    r.id          = get_string(obj, "id", "Id");
    r.name        = get_string(obj, "name", "Name");
    r.description = get_string(obj, "description", "Description");
    return r;
}

Script parse_script(cJSON* obj)
{
    Script s;
    s.name     = get_string(obj, "name", "Name");
    s.contents = get_string(obj, "contents", "Contents");

    cJSON* t = get_child(obj, "type", "Type");
    if (t && t->type == cJSON_Number) {
        switch (t->valueint) {
            case 0:  s.type = ScriptType::Install;      break;
            case 1:  s.type = ScriptType::Uninstall;    break;
            case 2:  s.type = ScriptType::NameChange;   break;
            case 3:  s.type = ScriptType::KeyChange;    break;
            case 4:  s.type = ScriptType::SaveUpload;   break;
            case 5:  s.type = ScriptType::SaveDownload; break;
            case 6:  s.type = ScriptType::DetectInstall; break;
            case 7:  s.type = ScriptType::BeforeStart;  break;
            case 8:  s.type = ScriptType::AfterStop;    break;
            case 9:  s.type = ScriptType::GameStarted;  break;
            case 10: s.type = ScriptType::GameStopped;  break;
            default: s.type = ScriptType::Unknown;      break;
        }
    } else if (t && t->type == cJSON_String && t->valuestring) {
        std::string sv = t->valuestring;
        if (sv == "Install")        s.type = ScriptType::Install;
        else if (sv == "Uninstall") s.type = ScriptType::Uninstall;
        else if (sv == "NameChange")  s.type = ScriptType::NameChange;
        else if (sv == "KeyChange")   s.type = ScriptType::KeyChange;
        else if (sv == "BeforeStart") s.type = ScriptType::BeforeStart;
        else if (sv == "AfterStop")   s.type = ScriptType::AfterStop;
        else s.type = ScriptType::Unknown;
    } else {
        s.type = ScriptType::Unknown;
    }
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
