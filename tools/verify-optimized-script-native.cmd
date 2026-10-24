@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
for %%p in (loop switch conditional constant division) do (
 pushd "%~dp0..\artifacts\%%p-statement-check"
 cl /nologo /EHsc /W4 /WX /O2 /std:c++20 /fp:precise native-main.cpp /Fe:optimized-check.exe
 if errorlevel 1 exit /b 1
 optimized-check.exe > native-optimized-actual.txt
 if errorlevel 1 exit /b 1
 fc expected.txt native-optimized-actual.txt
 if errorlevel 1 exit /b 1
 popd
)
exit /b 0
