@echo off
rem Builds both editions.
call "%~dp0build-zh.bat" || exit /b 1
call "%~dp0build-en.bat" || exit /b 1
