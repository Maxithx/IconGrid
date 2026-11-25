$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'IconGrid.csproj'
$publishDir = Join-Path $root '.local-state\installer\publish'
$installerDir = $PSScriptRoot
$iss = Join-Path $installerDir 'IconGrid.iss'

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

Write-Output 'Publishing IconGrid (framework-dependent, win-x64)...'
& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishDir `
    --nologo `
    -v minimal

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$fpsAgent = Join-Path $publishDir 'Tools\FpsAgent\IconGridFpsAgent.exe'
if (-not (Test-Path -LiteralPath $fpsAgent)) {
    throw "The native FPS agent is missing from the publish output: $fpsAgent"
}

$assemblyInfo = Get-Content -LiteralPath (Join-Path $root 'AssemblyInfo.cs') -Raw
$version = [regex]::Match($assemblyInfo, 'AssemblyInformationalVersion\("([^"]+)"\)').Groups[1].Value
$fileVersion = [regex]::Match($assemblyInfo, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = '0.0.0'
}
if ([string]::IsNullOrWhiteSpace($fileVersion)) {
    $fileVersion = '0.0.0.0'
}

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 ISCC.exe was not found. Install it with: winget install --id JRSoftware.InnoSetup --exact'
}

Write-Output "Compiling installer for IconGrid $version..."
& $iscc "/DMyAppVersion=$version" "/DMyAppFileVersion=$fileVersion" $iss
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}

$output = Join-Path $root ".local-state\installer\output\IconGrid-$version-Setup.exe"
if (-not (Test-Path -LiteralPath $output)) {
    throw "Installer output was not found: $output"
}

Write-Output "Installer created: $output"
Get-Item -LiteralPath $output | Select-Object Name, Length, LastWriteTime
