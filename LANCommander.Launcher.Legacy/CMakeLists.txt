cmake_minimum_required(VERSION 3.14)
project(LANCommander.Launcher.Legacy VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ---------------------------------------------------------------------------
# Options
# ---------------------------------------------------------------------------
option(ALLEGRO_STATIC "Link Allegro 4 statically" ON)
option(TARGET_WIN9X   "Target Windows 95/98 (subsystem 4.0, no DWM/GDI+ link)" OFF)

# ---------------------------------------------------------------------------
# Allegro 4
# ---------------------------------------------------------------------------
# Allegro 4 can be found via pkg-config or by setting ALLEGRO_ROOT.
# For static linking (typical on Win9x targets), set ALLEGRO_STATIC=ON.

if(ALLEGRO_STATIC)
    add_definitions(-DALLEGRO_STATICLINK)
endif()

# Try pkg-config first
find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
    if(ALLEGRO_STATIC)
        pkg_check_modules(ALLEGRO QUIET allegro-static)
    else()
        pkg_check_modules(ALLEGRO QUIET allegro)
    endif()
endif()

# Fallback: manual discovery via ALLEGRO_ROOT
if(NOT ALLEGRO_FOUND)
    if(DEFINED ALLEGRO_ROOT)
        set(ALLEGRO_INCLUDE_DIRS "${ALLEGRO_ROOT}/include")

        if(ALLEGRO_STATIC)
            find_library(ALLEGRO_LIB_RELEASE NAMES alleg_s alleg PATHS "${ALLEGRO_ROOT}/lib" NO_DEFAULT_PATH)
        else()
            find_library(ALLEGRO_LIB_RELEASE NAMES alleg PATHS "${ALLEGRO_ROOT}/lib" NO_DEFAULT_PATH)
        endif()

        if(ALLEGRO_LIB_RELEASE)
            set(ALLEGRO_LIBRARIES ${ALLEGRO_LIB_RELEASE})
            set(ALLEGRO_FOUND TRUE)
        endif()
    endif()
endif()

if(NOT ALLEGRO_FOUND)
    message(WARNING
        "Allegro 4 not found. Set ALLEGRO_ROOT or install Allegro 4 dev packages.\n"
        "  cmake -DALLEGRO_ROOT=/path/to/allegro4 ..")
endif()

# ---------------------------------------------------------------------------
# LANCommander C++ SDK (sibling directory)
# ---------------------------------------------------------------------------
add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/../LANCommander.SDK.Cpp
                 ${CMAKE_CURRENT_BINARY_DIR}/sdk)

# ---------------------------------------------------------------------------
# Launcher executable
# ---------------------------------------------------------------------------
add_executable(launcher WIN32
    src/launcher.rc
    src/main.cpp
    src/app/app.cpp
    src/app/settings.cpp
    src/app/download_queue.cpp
    src/app/game_database.cpp
    src/app/logger.cpp
    vendor/miniz/miniz.c
    vendor/miniz/miniz_tdef.c
    vendor/miniz/miniz_tinfl.c
    vendor/miniz/miniz_zip.c
    vendor/sqlite3/sqlite3.c
    src/ui/input.cpp
    src/ui/theme.cpp
    src/ui/widgets.cpp
    src/ui/screen_login.cpp
    src/ui/screen_library.cpp
    src/ui/screen_game_detail.cpp
    src/ui/screen_downloads.cpp
    src/ui/screen_settings.cpp
    src/ui/window_chrome.cpp
    src/ui/gdi_font.cpp
    src/ui/image_decoder.cpp
    src/ui/image_cache.cpp
)

target_include_directories(launcher PRIVATE
    src/
    vendor/miniz/
    vendor/sqlite3/
    ${ALLEGRO_INCLUDE_DIRS}
)

target_compile_definitions(launcher PRIVATE MINIZ_NO_EXPORT)

# Link the SDK statically — pulls in cjson automatically
target_link_libraries(launcher
    lancommander
)

# Link the WinINet HTTP backend on Windows
if(WIN32)
    target_link_libraries(launcher lancommander_wininet)
endif()

# Link Allegro
if(ALLEGRO_FOUND)
    target_link_libraries(launcher ${ALLEGRO_LIBRARIES})
endif()


# Platform libraries
if(WIN32)
    # Allegro 4 on Windows needs these system libraries.
    # dwmapi is loaded dynamically at runtime (Vista+) so we never link it.
    target_link_libraries(launcher
        gdi32
        gdiplus
        user32
        ole32
        dinput8
        ddraw
        dxguid
        winmm
        dsound
    )

    if(TARGET_WIN9X)
        target_compile_definitions(launcher PRIVATE
            WINVER=0x0400
            _WIN32_WINNT=0x0400
        )
        # PE subsystem 4.0 required for Win95/98 loader
        if(MSVC)
            target_link_options(launcher PRIVATE
                "/SUBSYSTEM:WINDOWS,4.0"
            )
        elseif(MINGW)
            target_link_options(launcher PRIVATE
                "-Wl,--subsystem,windows:4.0"
                "-static"
                "-static-libgcc"
                "-static-libstdc++"
            )
        endif()
    endif()
endif()
