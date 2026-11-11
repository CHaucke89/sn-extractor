@echo off
rem Builds SN Extractor.exe with the C# compiler built into Windows (nothing to install).
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:"SN-Extractor.exe" SNExtractor.cs
pause
