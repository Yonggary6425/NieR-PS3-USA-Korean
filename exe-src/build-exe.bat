@echo off
rem Build NieR-PS3-USA-Korean-Patcher.exe with the .NET Framework 4.x compiler included in Windows
setlocal
cd /d "%~dp0"
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 /out:NieR-PS3-USA-Korean-Patcher.exe ^
  /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll /r:System.Core.dll ^
  /resource:..\ps3-local-delta.zip,ps3-local-delta.zip ^
  Program.cs ..\NierPatch.cs ..\NierIso.cs
exit /b %errorlevel%
