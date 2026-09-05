param(
    [string]$Version = "0.1.0-preview.7",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$solutionDirectory = Join-Path $PSScriptRoot "..\VelocityGrid"
$artifactsDirectory = [IO.Path]::GetFullPath((Join-Path $solutionDirectory "artifacts"))
$packagesDirectory = [IO.Path]::GetFullPath((Join-Path $artifactsDirectory "packages"))
$consumerPackageCache = [IO.Path]::GetFullPath((Join-Path $artifactsDirectory "package-cache-$($Version.Replace('.', '_').Replace('-', '_'))"))
foreach ($directory in @($packagesDirectory, $consumerPackageCache)) {
    if (-not $directory.StartsWith($artifactsDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated output must remain inside the artifacts directory: $directory"
    }
}
if (Test-Path -LiteralPath $packagesDirectory) {
    Remove-Item -LiteralPath $packagesDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $packagesDirectory | Out-Null
if (Test-Path -LiteralPath $consumerPackageCache) {
    Remove-Item -LiteralPath $consumerPackageCache -Recurse -Force
}
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

dotnet build (Join-Path $solutionDirectory "PackageTests\CSharp.WinUI\CSharp.WinUI.csproj") -c $Configuration --no-cache "/p:VelocityGridPackageVersion=$Version" "/p:RestorePackagesPath=$consumerPackageCache"
if ($LASTEXITCODE -ne 0) { throw "C# package consumer failed." }
dotnet build (Join-Path $solutionDirectory "PackageTests\Wpf\Wpf.csproj") -c $Configuration --no-cache "/p:VelocityGridPackageVersion=$Version" "/p:RestorePackagesPath=$consumerPackageCache"
if ($LASTEXITCODE -ne 0) { throw "WPF package consumer failed." }
& $msbuild (Join-Path $solutionDirectory "PackageTests\Cpp.WinUI\Cpp.WinUI.vcxproj") /m:1 /nr:false /t:Restore /p:Configuration=Release /p:Platform=x64 "/p:VelocityGridPackageVersion=$Version" "/p:RestorePackagesPath=$consumerPackageCache" /v:minimal
if ($LASTEXITCODE -ne 0) { throw "C++ package consumer restore failed." }
& $msbuild (Join-Path $solutionDirectory "PackageTests\Cpp.WinUI\Cpp.WinUI.vcxproj") /m:1 /nr:false /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:VelocityGridPackageVersion=$Version" "/p:RestorePackagesPath=$consumerPackageCache" /v:minimal
if ($LASTEXITCODE -ne 0) { throw "C++ package consumer failed." }
$cppConsumerRuntime = Join-Path $solutionDirectory "PackageTests\Cpp.WinUI\bin\VelocityGrid.Native.dll"
if (-not (Test-Path -LiteralPath $cppConsumerRuntime)) {
    throw "C++ package consumer did not receive the app-local native runtime."
}

$expectedPackages = @(
    "VelocityGrid.Native.WinUI.$Version.nupkg",
    "VelocityGrid.WinUI.$Version.nupkg",
    "VelocityGrid.WinUI.$Version.snupkg",
    "VelocityGrid.Wpf.$Version.nupkg",
    "VelocityGrid.Wpf.$Version.snupkg"
)
$actualPackages = @(Get-ChildItem -LiteralPath $packagesDirectory -File | Select-Object -ExpandProperty Name)
$unexpectedPackages = @($actualPackages | Where-Object { $_ -notin $expectedPackages })
$missingPackages = @($expectedPackages | Where-Object { $_ -notin $actualPackages })
if ($unexpectedPackages.Count -ne 0 -or $missingPackages.Count -ne 0) {
    throw "Unexpected package output. Missing: $($missingPackages -join ', '). Unexpected: $($unexpectedPackages -join ', ')."
}

Write-Host "VelocityGrid packages $Version are in VelocityGrid\artifacts\packages."
