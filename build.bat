@echo off
rem Compila DiskUsage.exe con el compilador que ya trae Windows (no hay que instalar nada)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /optimize+ /target:winexe /codepage:65001 /out:DiskUsage.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll DiskUsage.cs
if errorlevel 1 (pause) else (echo OK: DiskUsage.exe creado & timeout /t 3 >nul)
