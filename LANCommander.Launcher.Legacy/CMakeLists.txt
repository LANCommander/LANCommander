cmake_minimum_required(VERSION 3.16)
project(LANCommander.Launcher.Legacy VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# SDL3 requires C99. The launcher's own C sources (miniz, sqlite3) previously
# took the compiler default.
set(CMAKE_C_STANDARD 99)

# ---------------------------------------------------------------------------
# Options
# ---------------------------------------------------------------------------
option(TARGET_WIN9X "Target Windows 95/98 (subsystem 4.0, no DWM/GDI+ link)" OFF)

# Off by default so the normal launcher build stays lean, matching
# liblancommander's LANCOMMANDER_BUILD_TESTS convention.
option(LAUNCHER_BUILD_TESTS "Build the launcher's headless unit tests" OFF)

set(LAUNCHER_GFX_BACKEND "sdl3" CACHE STRING
    "Graphics backend: 'sdl3' (current) or 'allegro' (transitional)")
set_property(CACHE LAUNCHER_GFX_BACKEND PROPERTY STRINGS allegro sdl3)

if(NOT LAUNCHER_GFX_BACKEND MATCHES "^(allegro|sdl3)$")
    message(FATAL_ERROR
        "LAUNCHER_GFX_BACKEND must be 'allegro' or 'sdl3', got '${LAUNCHER_GFX_BACKEND}'")
endif()

message(STATUS "Launcher graphics backend: ${LAUNCHER_GFX_BACKEND}")

# ---------------------------------------------------------------------------
# Backend: Allegro 4 (transitional)
# ---------------------------------------------------------------------------
# Kept only so the Win9x target stays buildable until the SDL3 Win9x backend
# runs on real hardware. Deleted after that.
if(LAUNCHER_GFX_BACKEND STREQUAL "allegro")
    option(ALLEGRO_STATIC "Link Allegro 4 statically" ON)

    if(ALLEGRO_STATIC)
        add_definitions(-DALLEGRO_STATICLINK)
    endif()

    find_package(PkgConfig QUIET)
    if(PKG_CONFIG_FOUND)
        if(ALLEGRO_STATIC)
            pkg_check_modules(ALLEGRO QUIET allegro-static)
        else()
            pkg_check_modules(ALLEGRO QUIET allegro)
        endif()
    endif()

    if(NOT ALLEGRO_FOUND AND DEFINED ALLEGRO_ROOT)
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

    if(NOT ALLEGRO_FOUND)
        message(FATAL_ERROR
            "Allegro 4 not found. Set ALLEGRO_ROOT or install Allegro 4 dev packages.\n"
            "  cmake -DALLEGRO_ROOT=/path/to/allegro4 ..")
    endif()

    set(LAUNCHER_GFX_SOURCES
        src/gfx/gfx_allegro.cpp
        src/ui/input_win32.cpp
        src/ui/chrome_platform_win32.cpp
        src/ui/clipboard_win32.cpp
        src/ui/font_gdi.cpp)
    set(LAUNCHER_BACKEND_DEFINE LAUNCHER_BACKEND_ALLEGRO)
endif()

# ---------------------------------------------------------------------------
# Backend: SDL3 + SDL_ttf + FreeType
# ---------------------------------------------------------------------------
if(LAUNCHER_GFX_BACKEND STREQUAL "sdl3")
    # FreeType lives at sdl_ttf/external/freetype, which is where SDL_ttf's
    # own vendoring expects it. Letting SDL_ttf add it keeps everything
    # target-based; the "system freetype" path instead runs
    # find_package(Freetype), which turns Freetype::Freetype into an imported
    # target pointing at a bare library name the linker cannot resolve.
    set(_ft_dir "${CMAKE_CURRENT_SOURCE_DIR}/vendor/sdl_ttf/external/freetype")

    if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/vendor/sdl3/CMakeLists.txt"
       OR NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/vendor/sdl_ttf/CMakeLists.txt"
       OR NOT EXISTS "${_ft_dir}/CMakeLists.txt")
        message(FATAL_ERROR
            "SDL3 / SDL_ttf / FreeType sources are missing. Fetch them with:\n"
            "  git submodule update --init --depth 1 -- \\\n"
            "      LANCommander.Launcher.Legacy/vendor/sdl3 \\\n"
            "      LANCommander.Launcher.Legacy/vendor/sdl_ttf\n"
            "  git -C LANCommander.Launcher.Legacy/vendor/sdl_ttf \\\n"
            "      submodule update --init --depth 1 -- external/freetype\n"
            "Never use --recursive: SDL_ttf also pins harfbuzz and plutosvg, "
            "which we deliberately do not build.")
    endif()

    # Everything is linked statically into a single self-contained EXE.
    # SDL_ttf keys off this to decide whether it needs SDL3-shared or
    # SDL3::SDL3, so it must be set before either subdirectory.
    set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)

    # SDL_ttf pins libsdl-org/freetype at VER-2-13-2-SDL, whose CMakeLists
    # still declares `cmake_minimum_required(VERSION 3.0...3.10)`. CMake 4.x
    # refuses anything below 3.5, so without this the configure fails outright.
    # (SDL3 and SDL_ttf themselves are fine; it is only the FreeType fork.)
    set(CMAKE_POLICY_VERSION_MINIMUM 3.5 CACHE STRING "" FORCE)

    # --- SDL3 -------------------------------------------------------------
    # The launcher composites into its own 32-bit surface and presents with a
    # single SDL_BlitSurface, so it needs video + events + timers and nothing
    # else. Everything below is dead weight on the target.
    set(SDL_SHARED       OFF CACHE BOOL "" FORCE)
    set(SDL_STATIC       ON  CACHE BOOL "" FORCE)
    set(SDL_TESTS        OFF CACHE BOOL "" FORCE)
    set(SDL_EXAMPLES     OFF CACHE BOOL "" FORCE)
    set(SDL_INSTALL      OFF CACHE BOOL "" FORCE)
    set(SDL_TEST_LIBRARY OFF CACHE BOOL "" FORCE)

    set(SDL_AUDIO    OFF CACHE BOOL "" FORCE)
    set(SDL_JOYSTICK OFF CACHE BOOL "" FORCE)
    set(SDL_HAPTIC   OFF CACHE BOOL "" FORCE)
    set(SDL_HIDAPI   OFF CACHE BOOL "" FORCE)
    set(SDL_SENSOR   OFF CACHE BOOL "" FORCE)
    set(SDL_CAMERA   OFF CACHE BOOL "" FORCE)
    set(SDL_POWER    OFF CACHE BOOL "" FORCE)
    set(SDL_DIALOG   OFF CACHE BOOL "" FORCE)
    set(SDL_GPU      OFF CACHE BOOL "" FORCE)
    set(SDL_VULKAN   OFF CACHE BOOL "" FORCE)

    set(SDL_RENDER_D3D    OFF CACHE BOOL "" FORCE)
    set(SDL_RENDER_D3D11  OFF CACHE BOOL "" FORCE)
    set(SDL_RENDER_D3D12  OFF CACHE BOOL "" FORCE)
    set(SDL_RENDER_VULKAN OFF CACHE BOOL "" FORCE)
    set(SDL_RENDER_GPU    OFF CACHE BOOL "" FORCE)

    add_subdirectory(vendor/sdl3 ${CMAKE_CURRENT_BINARY_DIR}/sdl3 EXCLUDE_FROM_ALL)

    if(TARGET_WIN9X)
        # Builds SDL's Windows backend against the ANSI entry points. The
        # W-suffixed ones are exported on 95/98 but fail at runtime, so a
        # UNICODE build links cleanly and then cannot create a window.
        # See the LANCommander/SDL fork, branch lancommander/win9x.
        #
        # PRIVATE because only SDL's own sources include SDL_windows.h, which
        # is where the switch is read.
        target_compile_definitions(SDL3-static PRIVATE SDL_WIN9X)
        message(STATUS "SDL3: building the Windows backend as ANSI (SDL_WIN9X)")
    endif()

    # --- SDL_ttf (+ FreeType) ---------------------------------------------
    # Prefix is SDLTTF_, not SDL3TTF_ — the latter is silently ignored.
    set(SDLTTF_VENDORED ON  CACHE BOOL "" FORCE)  # uses external/freetype
    set(SDLTTF_HARFBUZZ OFF CACHE BOOL "" FORCE)
    set(SDLTTF_PLUTOSVG OFF CACHE BOOL "" FORCE)
    set(SDLTTF_SAMPLES  OFF CACHE BOOL "" FORCE)  # need SDL_RENDER/SDL_GPU
    set(SDLTTF_INSTALL  OFF CACHE BOOL "" FORCE)

    add_subdirectory(vendor/sdl_ttf ${CMAKE_CURRENT_BINARY_DIR}/sdl_ttf EXCLUDE_FROM_ALL)

    # FreeType picks builds/windows/ftsystem.c unconditionally on WIN32 and
    # offers no option to opt out. That file defines its own GetFileSizeEx
    # shim over GetFileSize when _WIN32_WINNT <= 0x0400 (right for Win9x),
    # but the shim then collides at link time with the real GetFileSizeEx
    # that SDL3's SDL_iostream.c imports:
    #     multiple definition of `GetFileSizeEx@8'
    # Swap in FreeType's portable ANSI stdio implementation, which avoids the
    # clash and is also what a future DOS build needs. Done here rather than
    # as a patch so FreeType stays an unmodified checkout.
    #
    # Do NOT also define FT_CONFIG_OPTION_DISABLE_STREAM_SUPPORT: SDL_ttf's
    # TTF_OpenFont relies on FreeType stream support and every font load
    # fails with "Couldn't load font file" without it.
    if(WIN32 AND TARGET freetype)
        get_target_property(_ft_srcs freetype SOURCES)
        set(_ft_patched "")
        set(_ft_swapped FALSE)
        foreach(_s IN LISTS _ft_srcs)
            if(_s MATCHES "builds/windows/ftsystem\\.c$")
                list(APPEND _ft_patched "${_ft_dir}/src/base/ftsystem.c")
                set(_ft_swapped TRUE)
            else()
                list(APPEND _ft_patched "${_s}")
            endif()
        endforeach()
        if(_ft_swapped)
            set_target_properties(freetype PROPERTIES SOURCES "${_ft_patched}")
            message(STATUS "FreeType: using portable src/base/ftsystem.c")
        endif()
    endif()

    set(LAUNCHER_GFX_SOURCES
        src/gfx/gfx_sdl.cpp
        src/ui/input_sdl.cpp
        src/ui/chrome_platform_sdl.cpp
        src/ui/clipboard_sdl.cpp
        src/ui/font_ttf.cpp)
    set(LAUNCHER_BACKEND_DEFINE LAUNCHER_BACKEND_SDL)
endif()

# ---------------------------------------------------------------------------
# LANCommander C++ SDK (sibling directory)
# ---------------------------------------------------------------------------
add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/../liblancommander
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
    src/app/time_util.cpp
    src/app/media_prefetch.cpp
    src/app/game_art_fetcher.cpp
    src/app/library_sections.cpp
    src/app/depot_sections.cpp
    src/app/game_menu.cpp
    src/app/logger.cpp
    src/app/paths.cpp
    ${LAUNCHER_GFX_SOURCES}
    vendor/miniz/miniz.c
    vendor/miniz/miniz_tdef.c
    vendor/miniz/miniz_tinfl.c
    vendor/miniz/miniz_zip.c
    vendor/sqlite3/sqlite3.c
    vendor/stb/stb_impl.c
    src/ui/theme.cpp
    src/ui/widgets.cpp
    src/ui/widgets_text.cpp
    src/ui/text_edit.cpp
    src/ui/screen_login.cpp
    src/ui/screen_server_select.cpp
    src/ui/auth_background.cpp
    src/ui/screen_library.cpp
    src/ui/screen_depot.cpp
    src/ui/screen_depot_browse.cpp
    src/ui/screen_game_detail.cpp
    src/ui/screen_downloads.cpp
    src/ui/screen_settings.cpp
    src/ui/window_chrome.cpp
    src/ui/chrome_geometry.cpp
    src/ui/layout.cpp
    src/ui/widgets_overlay.cpp
    src/ui/widgets_collection.cpp
    src/ui/widgets_menu.cpp
    src/ui/image_decoder.cpp
    src/ui/icons.cpp
    src/ui/image_cache.cpp
)

target_include_directories(launcher PRIVATE
    src/
    vendor/miniz/
    vendor/sqlite3/
    vendor/stb/
    ${ALLEGRO_INCLUDE_DIRS}
)

# Assets live next to the executable and are loaded by path at runtime.
add_custom_command(TARGET launcher POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
            "${CMAKE_CURRENT_SOURCE_DIR}/assets"
            "$<TARGET_FILE_DIR:launcher>/assets"
    COMMENT "Copying assets next to launcher.exe")

target_compile_definitions(launcher PRIVATE MINIZ_NO_EXPORT ${LAUNCHER_BACKEND_DEFINE})

# Link the SDK statically — pulls in cjson automatically
target_link_libraries(launcher
    lancommander
)

# Link the WinINet HTTP backend on Windows
if(WIN32)
    target_link_libraries(launcher lancommander_wininet)
endif()

# Backend libraries
if(LAUNCHER_GFX_BACKEND STREQUAL "allegro")
    target_link_libraries(launcher ${ALLEGRO_LIBRARIES})
else()
    target_link_libraries(launcher SDL3_ttf::SDL3_ttf-static SDL3::SDL3-static)
endif()


# Platform libraries
if(WIN32)
    # gdiplus is deliberately absent: image decoding moved to stb, which is
    # what lets out-win9x/ stop shipping the gdiplus.dll redistributable.
    # gdi32 remains only for the transitional GDI text renderer.
    target_link_libraries(launcher
        gdi32
        user32
        ole32
        winmm
    )

    # Allegro 4 on Windows additionally needs the DirectX import libraries.
    # dwmapi is loaded dynamically at runtime (Vista+) so we never link it.
    if(LAUNCHER_GFX_BACKEND STREQUAL "allegro")
        target_link_libraries(launcher dinput8 ddraw dxguid dsound)
    endif()

    # Static runtime, so launcher.exe is the only file that has to be copied.
    #
    # A default MinGW link leaves libgcc_s_dw2-1.dll and libstdc++-6.dll as
    # external imports; neither ships with Windows, so the launcher would fail
    # to start anywhere the MSYS2 bin directory was not on PATH. These flags
    # used to be set only for TARGET_WIN9X, which meant the normal build was
    # the one that needed DLLs alongside it.
    #
    # Everything else the binary imports is a Windows system DLL, msvcrt.dll
    # included -- that is the CRT MinGW targets and it is present on every
    # Windows the launcher supports.
    if(MINGW)
        target_link_options(launcher PRIVATE
            "-static"
            "-static-libgcc"
            "-static-libstdc++"
        )
    endif()

    if(TARGET_WIN9X)
        target_compile_definitions(launcher PRIVATE
            WINVER=0x0400
            _WIN32_WINNT=0x0400
            # stb auto-detects and emits SSE2 (AVX2 in resize2). Win9x-era
            # targets are Pentium/PII/K6 class, so an SSE2 instruction is an
            # illegal opcode at runtime.
            STBI_NO_SIMD
            STBIR_NO_SIMD
        )
        # PE subsystem 4.0 required for Win95/98 loader
        if(MSVC)
            target_link_options(launcher PRIVATE
                "/SUBSYSTEM:WINDOWS,4.0"
            )
        elseif(MINGW)
            # The static flags are set for every MinGW build above; this adds
            # only the PE subsystem version the Win9x loader requires.
            target_link_options(launcher PRIVATE
                "-Wl,--subsystem,windows:4.0"
            )
        endif()
    endif()
endif()
