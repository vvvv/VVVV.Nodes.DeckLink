@echo off

set rootDir=%~dp0
set releasePath=%rootDir%Release\nodes\plugins
set warning=Please provide the plugins name as an argument
set pluginname=%1

if [%pluginname%] == [] (
   @echo on
   echo %warning%
   @echo off
   set /p id=Press any key to exit...
) else (
  @echo off
  setlocal enableextensions enabledelayedexpansion
  md %releasePath%\%pluginname%
  endlocal

  rem /mir mirrors the given directory tree
  xcopy /s %rootDir%Patches\* %releasePath%\%pluginname%\
  md %releasePath%\%pluginname%\dx11\
  xcopy /s %rootDir%VVVV.Nodes.DeckLink\effects\* %releasePath%\%pluginname%\dx11\

  @echo on
  echo Packaged release into %releasePath%
)

