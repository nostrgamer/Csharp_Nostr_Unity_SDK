@echo off
echo Creating signature test project...

rem Clean up existing project if it exists
if exist SignatureTestProject (
    echo Cleaning up existing project...
    rmdir /s /q SignatureTestProject
)

rem Create a new class library project
dotnet new classlib -o SignatureTestProject --force
cd SignatureTestProject

rem Create a simple project file targeting .NET Standard 2.0
echo ^<Project Sdk="Microsoft.NET.Sdk"^> > SignatureTestProject.csproj
echo   ^<PropertyGroup^> >> SignatureTestProject.csproj
echo     ^<TargetFramework^>netstandard2.0^</TargetFramework^> >> SignatureTestProject.csproj
echo     ^<LangVersion^>8.0^</LangVersion^> >> SignatureTestProject.csproj
echo   ^</PropertyGroup^> >> SignatureTestProject.csproj
echo   ^<ItemGroup^> >> SignatureTestProject.csproj
echo     ^<PackageReference Include="BouncyCastle.NetCore" Version="2.2.1" /^> >> SignatureTestProject.csproj
echo     ^<PackageReference Include="Newtonsoft.Json" Version="13.0.3" /^> >> SignatureTestProject.csproj
echo   ^</ItemGroup^> >> SignatureTestProject.csproj
echo ^</Project^> >> SignatureTestProject.csproj

rem Copy the test file
copy ..\SignatureTest.cs Program.cs /Y

rem Build the project
echo Building test library...
dotnet build

cd .. 