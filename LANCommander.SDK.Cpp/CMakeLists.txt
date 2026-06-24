cmake_minimum_required(VERSION 3.14)
project(lancommander VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ---------------------------------------------------------------------------
# Vendored dependencies
# ---------------------------------------------------------------------------
add_library(cjson STATIC vendor/cjson/cJSON.c)
target_include_directories(cjson PUBLIC vendor/cjson)
target_compile_definitions(cjson PRIVATE CJSON_HIDE_SYMBOLS)

# ---------------------------------------------------------------------------
# Core library (no HTTP backend — consumer must link one)
# ---------------------------------------------------------------------------
add_library(lancommander STATIC
    src/json/json_helpers.cpp
    src/clients/authentication_client.cpp
    src/clients/game_client.cpp
    src/clients/library_client.cpp
    src/clients/key_client.cpp
    src/clients/media_client.cpp
    src/clients/profile_client.cpp
    src/clients/save_client.cpp
    src/clients/script_client.cpp
    src/clients/redistributable_client.cpp
    src/clients/beacon_client.cpp
    src/clients/tool_client.cpp
    src/clients/launcher_client.cpp
    src/clients/depot_client.cpp
    src/clients/play_session_client.cpp
    src/clients/issue_client.cpp
    src/clients/connection_client.cpp
    src/archive/crc32_util.cpp
    src/script/batch_script_runner.cpp
)

target_include_directories(lancommander
    PUBLIC  include/
    PRIVATE src/    # for json/ internal headers
)

target_link_libraries(lancommander PUBLIC cjson)

# Platform socket libraries for BeaconClient
if(WIN32)
    target_link_libraries(lancommander PRIVATE ws2_32)
endif()

# ---------------------------------------------------------------------------
# WinINet backend (Windows only)
# ---------------------------------------------------------------------------
if(WIN32)
    add_library(lancommander_wininet STATIC
        backends/wininet/wininet_http_client.cpp
    )
    target_include_directories(lancommander_wininet
        PUBLIC  backends/wininet/
        PRIVATE include/
    )
    target_link_libraries(lancommander_wininet PUBLIC lancommander wininet)
endif()

# ---------------------------------------------------------------------------
# libcurl backend (optional — only if curl is found)
# ---------------------------------------------------------------------------
find_package(CURL QUIET)
if(CURL_FOUND)
    add_library(lancommander_curl STATIC
        backends/curl/curl_http_client.cpp
    )
    target_include_directories(lancommander_curl
        PUBLIC  backends/curl/
        PRIVATE include/
    )
    target_link_libraries(lancommander_curl PUBLIC lancommander CURL::libcurl)
endif()
