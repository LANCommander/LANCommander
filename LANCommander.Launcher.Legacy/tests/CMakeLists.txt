# Headless unit tests for the launcher.
#
# Standalone on purpose: this builds without a graphics backend, without the
# SDK, and without Windows, so it runs on the Linux CI runner where the Win9x
# job cannot execute what it produces.
#
#   cmake -S LANCommander.Launcher.Legacy/tests -B build-tests
#   cmake --build build-tests && ctest --test-dir build-tests --output-on-failure
#
# It is also picked up by the parent build via -DLAUNCHER_BUILD_TESTS=ON.

cmake_minimum_required(VERSION 3.16)

if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    project(launcher_tests CXX)
    set(CMAKE_CXX_STANDARD 14)
    set(CMAKE_CXX_STANDARD_REQUIRED ON)
    enable_testing()
endif()

set(LAUNCHER_SRC "${CMAKE_CURRENT_SOURCE_DIR}/..")

add_executable(launcher_tests
    test_main.cpp
    test_chrome_geometry.cpp
    test_text_wrap.cpp
    test_gfx_types.cpp

    # Code under test. Only files with no platform or backend dependency —
    # that boundary is exactly what the gfx seam bought.
    "${LAUNCHER_SRC}/src/ui/chrome_geometry.cpp"
    "${LAUNCHER_SRC}/src/ui/theme.cpp"

    # Headless stand-ins for the two things theme.cpp needs.
    "${LAUNCHER_SRC}/src/gfx/gfx_null.cpp"
    "${LAUNCHER_SRC}/src/ui/font_fake.cpp"
)

target_include_directories(launcher_tests PRIVATE
    "${LAUNCHER_SRC}/src"
    "${CMAKE_CURRENT_SOURCE_DIR}"
)

add_test(NAME launcher_tests COMMAND launcher_tests)
