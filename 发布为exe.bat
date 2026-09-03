@echo off
REM Wrapper: keep old Chinese filename; real script is ASCII publish-exe.bat
cd /d "%~dp0"
call "%~dp0publish-exe.bat"
