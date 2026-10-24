@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0..\artifacts\converter-check"
cl /nologo /EHsc /W4 /WX /fp:strict main.cpp /Fe:native-check.exe
if errorlevel 1 (popd & exit /b 1)
native-check.exe > actual.txt
if errorlevel 1 (popd & exit /b 1)
fc expected.txt actual.txt
set "checkExit=%errorlevel%"
popd
exit /b %checkExit%
