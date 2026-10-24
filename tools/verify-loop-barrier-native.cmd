@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0..\artifacts\loop-statement-check"
cl /nologo /EHsc /W4 /WX /O2 /fp:precise native-barrier.cpp /Fe:loop-barrier-check.exe
if errorlevel 1 (popd & exit /b 1)
loop-barrier-check.exe > native-barrier-actual.txt
if errorlevel 1 (popd & exit /b 1)
fc expected.txt native-barrier-actual.txt
set "checkExit=%errorlevel%"
popd
exit /b %checkExit%


