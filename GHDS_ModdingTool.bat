@echo off
set "msbuild=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\msbuild"
"%msbuild%" GHDS_ModdingTool.csproj
pause