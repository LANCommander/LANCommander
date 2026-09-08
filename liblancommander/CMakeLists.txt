cmake_minimum_required(VERSION 3.14)
project(lancommander VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ---------------------------------------------------------------------------
# MS-DOS (DJGPP)
# ---------------------------------------------------------------------------
# The cross toolchain leaves CMAKE_SYSTEM_NAME as Generic, so the target is
# detected from the compiler. cmake/toolchain-djgpp.cmake also sets
# LANCOMMANDER_TARGET_DOS; this makes a standalone configure work without it.
include(CheckCSourceCompiles)
check_c_source_compiles(
    "#ifndef __DJGPP__
#error not djgpp
#endif
int main(void){return 0;}"
    LANCOMMANDER_COMPILER_IS_DJGPP)

if(LANCOMMANDER_COMPILER_IS_DJGPP)
    set(LANCOMMANDER_TARGET_DOS TRUE)
endif()

# Watt-32 is the only TCP/IP stack available to a DOS program, and it is a
# separate build (see LANCommander.Launcher.Legacy/tools/setup-watt32.sh).
# Without it the SDK still builds; it just has no HTTP backend to offer.
set(WATT32_ROOT "" CACHE PATH
    "Watt-32 checkout containing inc/ and lib/libwatt.a (MS-DOS builds)")

if(LANCOMMANDER_TARGET_DOS AND WATT32_ROOT)
    set(PICOPOSH_WATT32_ROOT "${WATT32_ROOT}" CACHE PATH "" FORCE)
endif()

# ---------------------------------------------------------------------------
# Vendored dependencies
# ---------------------------------------------------------------------------
add_library(cjson STATIC vendor/cjson/cJSON.c)
target_include_directories(cjson PUBLIC vendor/cjson)
target_compile_definitions(cjson PRIVATE CJSON_HIDE_SYMBOLS)

# libyaml — needed to read the Manifest.yml the .NET launcher writes, so both
# launchers can share an install directory. Its version macros normally come
# from an autotools-generated config.h; we supply them directly instead.
add_library(yaml STATIC
    vendor/libyaml/api.c     vendor/libyaml/dumper.c
    vendor/libyaml/emitter.c vendor/libyaml/loader.c
    vendor/libyaml/parser.c  vendor/libyaml/reader.c
    vendor/libyaml/scanner.c vendor/libyaml/writer.c
)
target_include_directories(yaml PUBLIC vendor/libyaml)
target_compile_definitions(yaml PUBLIC YAML_DECLARE_STATIC)
target_compile_definitions(yaml PRIVATE
    YAML_VERSION_STRING="0.2.5"
    YAML_VERSION_MAJOR=0
    YAML_VERSION_MINOR=2
    YAML_VERSION_PATCH=5
)
if(MSVC)
    target_compile_definitions(yaml PRIVATE _CRT_SECURE_NO_WARNINGS)
endif()

# ---------------------------------------------------------------------------
# picoposh — embedded PowerShell-subset interpreter (git submodule).
#
# Consumed via add_subdirectory. Upstream resolves its paths through
# PROJECT_SOURCE_DIR and gates -Werror, its CLI target, CTest and its
# install/export rules behind options that default off when it is vendored, so
# none of that leaks into our build or a consumer's.
#
# We used to compile its sources ourselves, reading build/sources.list. That
# stopped being viable once picoposh grew five families of per-platform backend
# (directory, HTTP, registry, process, ZIP) that sources.list deliberately
# excludes — mirroring that selection by hand is exactly the drift it was
# supposed to avoid.
#
# Expand-Archive needs picoposh's own zlib and libzip submodules. When they are
# absent it links a stub and reports cleanly, so a shallow clone still builds —
# it just cannot unpack archives. See setup-vendor.ps1.
# ---------------------------------------------------------------------------
set(PICOPOSH_DIR ${CMAKE_CURRENT_SOURCE_DIR}/vendor/picoposh)

if(NOT EXISTS ${PICOPOSH_DIR}/CMakeLists.txt)
    message(FATAL_ERROR
        "vendor/picoposh is empty. Initialise the submodule:\n"
        "  git submodule update --init liblancommander/vendor/picoposh\n"
        "or run ./setup-vendor.ps1 from the repository root.")
endif()

add_subdirectory(${PICOPOSH_DIR} ${CMAKE_CURRENT_BINARY_DIR}/picoposh)

# picoposh vendors zlib and libzip for Expand-Archive, and defines a `zip`
# target for them. ZipArchiveExtractor links that same target rather than
# vendoring a second zip implementation — one in the process, not two.
#
# When the submodules are absent picoposh links its stub, no `zip` target
# exists, and the extractor compiles to a matching stub that reports cleanly.
if(TARGET zip)
    set(LANCOMMANDER_ZIP_LIBS zip)
    message(STATUS "lancommander: zip extraction via picoposh's libzip")
else()
    set(LANCOMMANDER_ZIP_LIBS "")
    message(STATUS
        "lancommander: no zip backend — ZipArchiveExtractor and Expand-Archive "
        "will report unavailable. Run setup-vendor.ps1 to fetch zlib/libzip.")
endif()

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
    src/archive/zip_archive_extractor.cpp
    src/json/json_writer.cpp
    src/yaml/yaml_convert.cpp
    src/manifest_helper.cpp
    src/util/path_util.cpp
    src/util/base64.cpp
    src/util/uri_util.cpp
    src/script/script_helper.cpp
    src/script/picoposh_script_runner.cpp
    src/clients/script_execution_client.cpp
    src/script/cmdlets/cmdlets.cpp
    src/script/cmdlets/cmdlet_support.cpp
    src/script/cmdlets/cmdlets_system.cpp
    src/script/cmdlets/cmdlets_math.cpp
    src/script/cmdlets/cmdlets_bytes.cpp
    src/script/cmdlets/cmdlets_text.cpp
    src/script/cmdlets/cmdlets_manifest.cpp
    src/script/cmdlets/cmdlets_api.cpp
    src/script/cmdlets/cmdlets_serialize.cpp
    src/script/cmdlets/cmdlets_options.cpp
    src/script/cmdlets/cmdlets_gamespy.cpp
)

target_include_directories(lancommander
    PUBLIC  include/
    PRIVATE src/    # for json/ internal headers
)

# PUBLIC on both: lancommander is a static library, so consumers must see these
# on their own link line to resolve cJSON_* and the pico_* entry points.
target_link_libraries(lancommander PUBLIC cjson yaml picoposh::picoposh)

if(LANCOMMANDER_ZIP_LIBS)
    target_compile_definitions(lancommander PRIVATE LANCOMMANDER_HAVE_LIBZIP)
    # PRIVATE + BUILD_INTERFACE, matching how picoposh consumes it: the
    # vendored target is not part of anyone's install/export set.
    target_link_libraries(lancommander
        PRIVATE $<BUILD_INTERFACE:${LANCOMMANDER_ZIP_LIBS}>)
endif()

# Platform socket libraries for BeaconClient
if(WIN32)
    target_link_libraries(lancommander PRIVATE ws2_32)
elseif(LANCOMMANDER_TARGET_DOS AND WATT32_ROOT)
    # PUBLIC: lancommander is static, so the consumer's link line is where
    # socket() and sock_init() actually have to resolve.
    target_include_directories(lancommander PUBLIC "${WATT32_ROOT}/inc")
    target_link_libraries(lancommander PUBLIC "${WATT32_ROOT}/lib/libwatt.a")
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
# Watt-32 backend (MS-DOS only)
# ---------------------------------------------------------------------------
if(LANCOMMANDER_TARGET_DOS)
    if(WATT32_ROOT AND EXISTS "${WATT32_ROOT}/inc/tcp.h")
        add_library(lancommander_watt32 STATIC
            backends/watt32/watt32_http_client.cpp
        )
        target_include_directories(lancommander_watt32
            PUBLIC  backends/watt32/
            PRIVATE include/
        )
        target_link_libraries(lancommander_watt32 PUBLIC lancommander)
        message(STATUS "lancommander: HTTP over Watt-32 (MS-DOS)")
    else()
        message(FATAL_ERROR
            "MS-DOS build with no Watt-32. There is no other TCP/IP stack on "
            "DOS, so the launcher would have no way to reach a server.
"
            "Build it with LANCommander.Launcher.Legacy/tools/setup-watt32.sh "
            "and pass -DWATT32_ROOT=<dir>.")
    endif()
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

# ---------------------------------------------------------------------------
# Tests and examples — off by default so add_subdirectory() consumers (the
# Legacy launcher) stay lean and never inherit enable_testing().
# ---------------------------------------------------------------------------
option(LANCOMMANDER_BUILD_TESTS "Build the LANCommander C++ SDK tests" OFF)
option(LANCOMMANDER_BUILD_EXAMPLES "Build the LANCommander C++ SDK examples" OFF)

if(LANCOMMANDER_BUILD_TESTS)
    enable_testing()

    add_executable(lancommander_tests
        tests/test_main.cpp
        tests/test_ps_quote.cpp
        tests/test_script_runner.cpp
        tests/test_script_helper.cpp
        tests/test_json.cpp
        tests/test_yaml.cpp
        tests/test_cmdlets.cpp
        tests/test_cmdlets_api.cpp
        tests/test_archive.cpp
        tests/test_serialize.cpp
        tests/test_options.cpp
        tests/test_gamespy.cpp
        tests/test_uri_candidates.cpp
    )
    target_link_libraries(lancommander_tests PRIVATE lancommander)
    # src/ for the internal json/ headers, vendor/cjson for cJSON.h.
    target_include_directories(lancommander_tests PRIVATE src/ vendor/cjson)

    add_test(NAME lancommander_tests COMMAND lancommander_tests)
endif()

if(LANCOMMANDER_BUILD_EXAMPLES)
    add_executable(script_runner_demo examples/script_runner_demo.cpp)
    target_link_libraries(script_runner_demo PRIVATE lancommander)
endif()
