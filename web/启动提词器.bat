@echo off
setlocal enabledelayedexpansion

set "HERE=%~dp0"
set "PAGE="
for %%f in ("%HERE%*.html") do if not defined PAGE set "PAGE=%%~ff"
if not defined PAGE goto nopage

set "URLF=%PAGE:\=/%"

rem ---- find browser: Chrome first, then Edge ----
set "B="
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "B=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not defined B if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "B=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not defined B if exist "%LocalAppData%\Google\Chrome\Application\chrome.exe" set "B=%LocalAppData%\Google\Chrome\Application\chrome.exe"
if not defined B if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" set "B=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if not defined B if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" set "B=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"

if not defined B (
  start "" "%PAGE%"
  exit /b
)

start "" "%B%" --app="file:///%URLF%" --user-data-dir="%HERE%_profile" --start-fullscreen --no-first-run --no-default-browser-check --disable-features=Translate
exit /b

:nopage
echo Cannot find the .html file. Please keep this .bat in the same folder as the .html.
pause
