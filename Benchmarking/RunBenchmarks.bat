@echo off
%~d0
cd "%~p0"

echo on
dotnet run -c release --framework netcoreapp2.1