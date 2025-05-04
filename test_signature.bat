@echo off
echo Creating signature test project...

rem Create a new project
dotnet new console -o SignatureTestProject
cd SignatureTestProject

rem Add BouncyCastle reference
dotnet add package BouncyCastle.NetCore

rem Copy the test file
copy ..\SignatureTest.cs Program.cs /Y

rem Build and run
echo Building and running test...
dotnet build
dotnet run

cd .. 