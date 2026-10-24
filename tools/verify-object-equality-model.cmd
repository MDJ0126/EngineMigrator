@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
pushd "%~dp0..\artifacts\object-equality-check"
cl /nologo /EHsc /W4 /WX /Od /std:c++20 native-model.cpp /Fe:native-model-Od.exe /Fo:native-model-Od.obj
if errorlevel 1 exit /b 1
native-model-Od.exe >native-model-Od.json
if errorlevel 1 exit /b 1
cl /nologo /EHsc /W4 /WX /O2 /std:c++20 native-model.cpp /Fe:native-model-O2.exe /Fo:native-model-O2.obj
if errorlevel 1 exit /b 1
native-model-O2.exe >native-model-O2.json
if errorlevel 1 exit /b 1
cl /nologo /EHsc /W4 /WX /Od /std:c++20 native-gc-model.cpp /Fe:native-gc-model-Od.exe /Fo:native-gc-model-Od.obj
if errorlevel 1 exit /b 1
native-gc-model-Od.exe >native-gc-model-Od.json
if errorlevel 1 exit /b 1
cl /nologo /EHsc /W4 /WX /O2 /std:c++20 native-gc-model.cpp /Fe:native-gc-model-O2.exe /Fo:native-gc-model-O2.obj
if errorlevel 1 exit /b 1
native-gc-model-O2.exe >native-gc-model-O2.json
if errorlevel 1 exit /b 1
popd
