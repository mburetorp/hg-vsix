@echo off
setlocal

rem Builds HgVsix.sln with Visual Studio 2026 MSBuild
rem Usage: build.bat [Release|Debug]   default Release
rem Output: build\bin\HgVsix\<config>\net472\HgVsix.vsix

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo vswhere.exe not found. Is Visual Studio 2026 installed?
    exit /b 1
)

rem Prefer an instance with the extension development workload
set "MSBUILD="
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -version [18.0^,19.0^) -requires Microsoft.VisualStudio.Workload.VisualStudioExtension -find MSBuild\**\Bin\MSBuild.exe`) do if not defined MSBUILD set "MSBUILD=%%i"
if not defined MSBUILD for /f "usebackq delims=" %%i in (`"%VSWHERE%" -version [18.0^,19.0^) -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined MSBUILD set "MSBUILD=%%i"
if not defined MSBUILD (
    echo MSBuild from Visual Studio 2026 not found.
    exit /b 1
)

echo Using %MSBUILD%
"%MSBUILD%" "%~dp0HgVsix.sln" -restore -p:Configuration=%CONFIG% -v:m -nologo
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo.
echo Built %~dp0build\bin\HgVsix\%CONFIG%\net472\HgVsix.vsix
exit /b 0
