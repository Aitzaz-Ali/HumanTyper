@echo off
rem Compiles HumanTyper.cs into HumanTyper.exe using the .NET Framework compiler built into Windows.
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize /out:"%~dp0HumanTyper.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0HumanTyper.cs"
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo Built HumanTyper.exe
