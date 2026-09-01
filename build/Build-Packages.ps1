param(
    [string]$Version = "0.1.0-preview.9",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$solutionDirectory = Join-Path $PSScriptRoot "..\VelocityGrid"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\amd64\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "Visual Studio MSBuild was not found." }

function Invoke-Build([string]$Project, [string]$Platform, [string]$Targets = "Build") {
    & $msbuild (Join-Path $solutionDirectory $Project) /m:1 /nr:false "/t:$Targets" "/p:Configuration=$Configuration" "/p:Platform=$Platform" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Project ($Platform)" }
}

Invoke-Build "VelocityGrid.Native\VelocityGrid.Native.vcxproj" "Win32"
Invoke-Build "VelocityGrid.Native\VelocityGrid.Native.vcxproj" "x64"
Invoke-Build "VelocityGrid.Native\VelocityGrid.Native.vcxproj" "ARM64"
Invoke-Build "VelocityGrid.Managed\VelocityGrid.Managed.csproj" "x64"
Invoke-Build "VelocityGrid.Wpf\VelocityGrid.Wpf.csproj" "x64"

foreach ($project in @(
    "VelocityGrid.Native.Packaging\VelocityGrid.Native.Packaging.csproj",
    "VelocityGrid.Packaging\VelocityGrid.Packaging.csproj",
    "VelocityGrid.Wpf.Packaging\VelocityGrid.Wpf.Packaging.csproj")) {
    & $msbuild (Join-Path $solutionDirectory $project) /m:1 /nr:false /t:Restore,Pack "/p:Configuration=$Configuration" "/p:VelocityGridPackageVersion=$Version" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $project" }
}

dotnet build (Join-Path $solutionDirectory "PackageTests\CSharp.WinUI\CSharp.WinUI.csproj") -c $Configuration --no-cache "/p:VelocityGridPackageVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "C# package consumer failed." }
dotnet build (Join-Path $solutionDirectory "PackageTests\Wpf\Wpf.csproj") -c $Configuration --no-cache "/p:VelocityGridPackageVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "WPF package consumer failed." }
& $msbuild (Join-Path $solutionDirectory "PackageTests\Cpp.WinUI\Cpp.WinUI.vcxproj") /m:1 /nr:false /t:Restore,Rebuild /p:Configuration=Release /p:Platform=x64 "/p:VelocityGridPackageVersion=$Version" /v:minimal
if ($LASTEXITCODE -ne 0) { throw "C++ package consumer failed." }

Write-Host "VelocityGrid packages $Version are in VelocityGrid\artifacts\packages."
