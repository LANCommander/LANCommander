@echo off
setlocal enabledelayedexpansion

rem ---------------------------------------------------------------------------
rem run-dos.cmd -- start the DOS launcher under DOSBox-X.
rem
rem The DOS build is a real DOS program, so something has to be DOS for it to
rem run on. This finds a DOSBox-X, writes a config with the right absolute
rem mount path, and starts it.
rem
rem It does not build. Run ./build-dos.sh first (or after any change) --
rem this only launches whatever is in out-dos\.
rem
rem DOSBox-X is looked for in two places, in order:
rem   1. vendor\dosbox-x\   (a portable copy unpacked there; gitignored)
rem   2. PATH
rem ---------------------------------------------------------------------------

set "LAUNCHER_DIR=%~dp0.."
for %%I in ("%LAUNCHER_DIR%") do set "LAUNCHER_DIR=%%~fI"

set "OUT_DIR=%LAUNCHER_DIR%\out-dos"

if not exist "%OUT_DIR%\LANCMDR.EXE" (
    echo.
    echo   No DOS build found at:
    echo     %OUT_DIR%\LANCMDR.EXE
    echo.
    echo   Build it first, from a bash shell:
    echo     ./build-dos.sh
    echo.
    pause
    exit /b 1
)

rem --- Find DOSBox-X ---------------------------------------------------------
set "DOSBOX="

for %%P in (
    "%LAUNCHER_DIR%\vendor\dosbox-x\bin\x64\Release\dosbox-x.exe"
    "%LAUNCHER_DIR%\vendor\dosbox-x\bin\Win32\Release\dosbox-x.exe"
    "%LAUNCHER_DIR%\vendor\dosbox-x\dosbox-x.exe"
) do (
    if not defined DOSBOX if exist %%P set "DOSBOX=%%~P"
)

if not defined DOSBOX (
    for /f "delims=" %%P in ('where dosbox-x.exe 2^>nul') do (
        if not defined DOSBOX set "DOSBOX=%%P"
    )
)

if not defined DOSBOX (
    echo.
    echo   DOSBox-X was not found.
    echo.
    echo   Either install it and put it on PATH, or unpack a portable build
    echo   into:
    echo     %LAUNCHER_DIR%\vendor\dosbox-x\
    echo.
    echo   Portable builds: https://github.com/joncampbell123/dosbox-x/releases
    echo.
    pause
    exit /b 1
)

rem --- Write the config ------------------------------------------------------
rem
rem Generated rather than copied so the mount path is absolute and correct
rem wherever the repository lives. tools\dosbox-x-test.conf is the same
rem settings with a comment on each explaining what breaks without it.

set "CONF=%OUT_DIR%\dosbox-x.conf"

> "%CONF%" (
    echo [sdl]
    echo autolock=false
    echo output=surface
    echo.
    echo [dos]
    echo # The assets are not 8.3 names, so without LFN the launcher draws
    echo # its layout and no text or artwork at all.
    echo lfn=true
    echo ver=7.1
    echo.
    echo [dosbox]
    echo # S3 Trio is what provides VBE 2.0 with a linear frame buffer.
    echo machine=svga_s3
    echo memsize=63
    echo working directory option=noprompt
    echo.
    echo [cpu]
    echo cputype=pentium
    echo core=dynamic
    echo cycles=100000
    echo.
    echo [autoexec]
    echo mount c "%OUT_DIR%"
    echo c:
    echo LANCMDR.EXE
    echo exit
)

rem DOSBox-X resolves some of its own paths against the working directory.
pushd "%OUT_DIR%"
"%DOSBOX%" -conf "%CONF%"
popd

endlocal
