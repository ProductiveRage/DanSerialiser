@echo off
REM In case this has been run from explorer or something then navigate to the right drive and folder
%~d0
cd "%~p0\Benchmarking"

REM Turn echo back on so that anyone curious can see what command is run
echo on
dotnet run -c release --framework netcoreapp2.1

@echo off
REM Jump back up a folder after running so that it's possible to run this again if doing it from the command line
cd..