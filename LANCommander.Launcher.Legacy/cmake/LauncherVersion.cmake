# Derives the launcher's version from the repository's latest git tag.
#
# Release tags look like "v2.1.12". On an exact tag we report "2.1.12"; on a
# commit after one, `git describe` yields "v2.1.12-39-geaa9f520" and we keep
# that suffix so a dev build is never mistaken for the release it followed.
#
# Sets, in the caller's scope:
#   LAUNCHER_VERSION        full string, e.g. "2.1.12" or "2.1.12-39-geaa9f520"
#   LAUNCHER_VERSION_MAJOR  numeric
#   LAUNCHER_VERSION_MINOR  numeric
#   LAUNCHER_VERSION_PATCH  numeric
#   LAUNCHER_VERSION_TWEAK  commits since the tag (0 on an exact tag)
#
# The numeric quadruple exists for the Win32 VERSIONINFO resource, which cannot
# express a prerelease suffix.

# Bump this when tagging a release. It is only reached when the version cannot
# be derived: source archives with no .git, git missing from PATH, or a shallow
# CI clone that fetched no tags.
set(LAUNCHER_VERSION_FALLBACK "2.1.12")

function(_launcher_describe out_var)
    set(${out_var} "" PARENT_SCOPE)

    find_package(Git QUIET)

    if(NOT GIT_FOUND)
        return()
    endif()

    # --match keeps the release tag series from being shadowed by any other tag
    # that happens to be closer to HEAD.
    execute_process(
        COMMAND "${GIT_EXECUTABLE}" describe --tags --dirty --match "v[0-9]*"
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        OUTPUT_VARIABLE describe_output
        OUTPUT_STRIP_TRAILING_WHITESPACE
        ERROR_QUIET
        RESULT_VARIABLE describe_result)

    if(NOT describe_result EQUAL 0)
        return()
    endif()

    set(${out_var} "${describe_output}" PARENT_SCOPE)
endfunction()

function(launcher_resolve_version)
    _launcher_describe(described)

    if(described STREQUAL "")
        message(STATUS
            "Launcher version: no git tag available, falling back to ${LAUNCHER_VERSION_FALLBACK}")
        set(version "${LAUNCHER_VERSION_FALLBACK}")
    else()
        # Strip the tag's leading "v"; everything downstream wants a bare version.
        string(REGEX REPLACE "^v" "" version "${described}")
    endif()

    if(NOT version MATCHES "^([0-9]+)\\.([0-9]+)\\.([0-9]+)")
        message(WARNING
            "Launcher version '${version}' is not MAJOR.MINOR.PATCH; using ${LAUNCHER_VERSION_FALLBACK}")
        set(version "${LAUNCHER_VERSION_FALLBACK}")
        string(REGEX MATCH "^([0-9]+)\\.([0-9]+)\\.([0-9]+)" _ "${version}")
    endif()

    set(major "${CMAKE_MATCH_1}")
    set(minor "${CMAKE_MATCH_2}")
    set(patch "${CMAKE_MATCH_3}")

    # "-39-g<sha>" means 39 commits past the tag. Feed that to VERSIONINFO's
    # fourth field so two dev builds off the same tag differ in the binary's
    # file version, not just in the string.
    if(version MATCHES "-([0-9]+)-g[0-9a-f]+")
        set(tweak "${CMAKE_MATCH_1}")
    else()
        set(tweak "0")
    endif()

    set(LAUNCHER_VERSION       "${version}" PARENT_SCOPE)
    set(LAUNCHER_VERSION_MAJOR "${major}"   PARENT_SCOPE)
    set(LAUNCHER_VERSION_MINOR "${minor}"   PARENT_SCOPE)
    set(LAUNCHER_VERSION_PATCH "${patch}"   PARENT_SCOPE)
    set(LAUNCHER_VERSION_TWEAK "${tweak}"   PARENT_SCOPE)
endfunction()

# Re-run CMake when HEAD moves, so an incremental build after a commit or a
# branch switch does not keep stamping a stale version. Tagging the commit that
# is already checked out does not touch HEAD; that case needs a manual
# reconfigure.
function(launcher_watch_git_head)
    set(git_dir "${CMAKE_CURRENT_SOURCE_DIR}/../.git")

    # A worktree or submodule has a .git *file* pointing elsewhere. Not worth
    # resolving — those builds just reconfigure manually.
    if(IS_DIRECTORY "${git_dir}" AND EXISTS "${git_dir}/HEAD")
        set_property(DIRECTORY APPEND
            PROPERTY CMAKE_CONFIGURE_DEPENDS "${git_dir}/HEAD")
    endif()
endfunction()
