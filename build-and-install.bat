@echo off
setlocal

rem Builds HgVsix.vsix in Release and opens the Visual Studio extension installer for it.
rem The installer lets you pick the Visual Studio editions.

set "VSIX=%~dp0build\bin\HgVsix\Release\net472\HgVsix.vsix"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

call "%~dp0build.bat" Release
if errorlevel 1 exit /b 1

set "INSTALL_PATH="
for /f "usebackq delims=" %%p in (`call "%VSWHERE%" -version [18.0^,19.0^) -latest -requires Microsoft.VisualStudio.Component.CoreEditor -property installationPath`) do set "INSTALL_PATH=%%p"
if not defined INSTALL_PATH (
    echo Visual Studio 2026 not found.
    exit /b 1
)

echo.
echo Opening the extension installer
start "" "%INSTALL_PATH%\Common7\IDE\VSIXInstaller.exe" "%VSIX%"
exit /b 0
