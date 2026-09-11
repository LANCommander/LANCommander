# CMake toolchain for MS-DOS via DJGPP (i586-pc-msdosdjgpp).
#
#   cmake -S LANCommander.Launcher.Legacy -B build-dos -G Ninja \
#         -DCMAKE_TOOLCHAIN_FILE=LANCommander.Launcher.Legacy/cmake/toolchain-djgpp.cmake \
#         -DDJGPP_ROOT=/path/to/djgpp
#
# DJGPP is a 32-bit protected-mode toolchain: GCC 12 with a full libstdc++, so
# the launcher's C++14 stays as it is. The output is a go32-v2 COFF image with
# a real-mode stub that loads CWSDPMI.EXE, which therefore has to ship next to
# it. This is a different DOS target from picoposh's 16-bit OpenWatcom one --
# nothing about a 14k-line C++ GUI fits in real mode's 64K near-data group.

set(CMAKE_SYSTEM_NAME Generic)
set(CMAKE_SYSTEM_PROCESSOR i586)

# Marks the target for the project's own `if()`s. CMAKE_SYSTEM_NAME is Generic
# rather than MSDOS because CMake ships no Platform/MSDOS module, and Generic
# is what keeps it from assuming a hosted Unix or Windows underneath. Set as a
# plain variable *and* in the cache: the plain one is visible to the project
# scope this file is included from, the cached one survives into try_compile()'s
# separate project, which re-reads only the cache.
set(LANCOMMANDER_TARGET_DOS TRUE)
set(LANCOMMANDER_TARGET_DOS TRUE CACHE BOOL "Building for MS-DOS via DJGPP" FORCE)

# DJGPP_ROOT is the directory holding bin/i586-pc-msdosdjgpp-*. Falls back to
# the DJGPP environment variable that setup-djgpp.sh exports.
if(NOT DJGPP_ROOT)
    if(DEFINED ENV{DJGPP_ROOT})
        set(DJGPP_ROOT $ENV{DJGPP_ROOT})
    elseif(DEFINED ENV{DJGPP})
        set(DJGPP_ROOT $ENV{DJGPP})
    endif()
endif()

if(NOT DJGPP_ROOT)
    message(FATAL_ERROR
        "DJGPP_ROOT is not set. Fetch the toolchain with\n"
        "  LANCommander.Launcher.Legacy/tools/setup-djgpp.sh\n"
        "then pass -DDJGPP_ROOT=<dir> (the directory containing bin/).")
endif()

set(DJGPP_ROOT "${DJGPP_ROOT}" CACHE PATH "DJGPP toolchain prefix" FORCE)
set(DJGPP_PREFIX "${DJGPP_ROOT}/bin/i586-pc-msdosdjgpp-")

# The tool names need the host's executable suffix, not the target's: these are
# programs CMake runs on the build machine.
if(CMAKE_HOST_WIN32)
    set(DJGPP_HOST_EXE ".exe")
else()
    set(DJGPP_HOST_EXE "")
endif()

# .exe explicitly: CMAKE_SYSTEM_NAME Generic defaults to no suffix, and DOS
# will not run an extensionless file.
set(CMAKE_EXECUTABLE_SUFFIX     ".exe")
set(CMAKE_EXECUTABLE_SUFFIX_C   ".exe")
set(CMAKE_EXECUTABLE_SUFFIX_CXX ".exe")

set(CMAKE_C_COMPILER   "${DJGPP_PREFIX}gcc${DJGPP_HOST_EXE}")
set(CMAKE_CXX_COMPILER "${DJGPP_PREFIX}g++${DJGPP_HOST_EXE}")
set(CMAKE_AR           "${DJGPP_PREFIX}ar${DJGPP_HOST_EXE}"      CACHE FILEPATH "")
set(CMAKE_RANLIB       "${DJGPP_PREFIX}ranlib${DJGPP_HOST_EXE}"  CACHE FILEPATH "")
set(CMAKE_STRIP        "${DJGPP_PREFIX}strip${DJGPP_HOST_EXE}"   CACHE FILEPATH "")
set(CMAKE_OBJDUMP      "${DJGPP_PREFIX}objdump${DJGPP_HOST_EXE}" CACHE FILEPATH "")

# Deliberately NOT set to STATIC_LIBRARY, which is the usual reflex for a
# bare-metal-looking toolchain. DJGPP links ordinary executables perfectly
# well, and an archive-only probe would make every check_function_exists()
# compile without linking -- so each one reports success. libzip then believes
# DJGPP has _fseeki64, clonefile() and getprogname(), and fails to build on
# the first source that uses any of them.
# set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)

# 386 is the floor CWSDPMI itself needs; -march=i386 would cost the 486/Pentium
# instruction selection for no compatible-hardware gain, so i386 is the ABI
# baseline and the scheduler is told to assume a Pentium.
set(DJGPP_ARCH_FLAGS "-march=i386 -mtune=pentium")

set(CMAKE_C_FLAGS_INIT   "${DJGPP_ARCH_FLAGS}")
set(CMAKE_CXX_FLAGS_INIT "${DJGPP_ARCH_FLAGS}")

# Nothing on the host is a valid dependency for a DOS binary. Without this,
# find_package/find_library happily return an MSYS2 or Program Files hit.
set(CMAKE_FIND_ROOT_PATH "${DJGPP_ROOT}")
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM BOTH)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
