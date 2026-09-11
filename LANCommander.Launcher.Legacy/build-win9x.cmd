@echo off
rem ---------------------------------------------------------------------------
rem build-win9x.cmd
rem
rem Runs build-win9x.ps1 from cmd.exe, from a double-click, or from anywhere
rem that is not already a PowerShell prompt.
rem
rem -ExecutionPolicy Bypass applies to this one invocation only and changes
rem nothing machine-wide. It is here because the default policy on a desktop
rem Windows install refuses unsigned local scripts, and "cannot be loaded
rem because running scripts is disabled" is not a useful first impression of
rem a build system.
rem
rem Arguments pass straight through:
rem     build-win9x.cmd
rem     build-win9x.cmd -BuildType Debug -Jobs 4 -Backend sdl3
rem ---------------------------------------------------------------------------

setlocal

set "PS=powershell.exe"
where pwsh.exe >nul 2>&1 && set "PS=pwsh.exe"

"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-win9x.ps1" %*
set "RESULT=%ERRORLEVEL%"

rem A double-clicked window closes the instant the build ends, taking the
rem result with it. Explorer launches a .cmd as `cmd /c "<path>"` with no
rem arguments, so pause on exactly that shape and nothing else -- pausing
rem whenever /c is present would hang `cmd /c build-dos.cmd` in a CI job with
rem no one there to press a key. Set LANCOMMANDER_NO_PAUSE=1 to be certain.
if not "%~1"=="" goto :done
if defined LANCOMMANDER_NO_PAUSE goto :done
echo %CMDCMDLINE% | find /i "/c" >nul && pause

:done
exit /b %RESULT%
