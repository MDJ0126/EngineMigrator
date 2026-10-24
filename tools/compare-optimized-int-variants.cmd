@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
pushd "%~dp0..\artifacts\loop-statement-check"
for %%v in (cast volatile bitcast) do (
 cl /nologo /EHsc /W4 /WX /O2 /std:c++20 /fp:precise native-%%v.cpp /Fe:native-%%v.exe
 if errorlevel 1 exit /b 1
 native-%%v.exe
)
popd
