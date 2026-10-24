@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0..\artifacts\blueprint-check"
cl /nologo /EHsc /W4 /WX /fp:strict loop-main.cpp /Fe:loop-check.exe
if errorlevel 1 (popd & exit /b 1)
loop-check.exe > loop-actual.txt
if errorlevel 1 (popd & exit /b 1)
fc loop-expected.txt loop-actual.txt
set "checkExit=%errorlevel%"
popd
exit /b %checkExit%
