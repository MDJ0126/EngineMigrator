@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0..\artifacts\constant-statement-check"
cl /nologo /EHsc /W4 /WX /fp:strict native-main.cpp /Fe:constant-check.exe
if errorlevel 1 (popd & exit /b 1)
constant-check.exe > native-actual.txt
if errorlevel 1 (popd & exit /b 1)
fc expected.txt native-actual.txt
set "checkExit=%errorlevel%"
popd
exit /b %checkExit%
