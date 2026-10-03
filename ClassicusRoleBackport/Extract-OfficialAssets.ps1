param(
    [string]$ModernContent = "C:\XboxGames\Among Us\Content"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$PluginDir = $PSScriptRoot
$OutputDir = Join-Path $PluginDir "OfficialAssets"
$WorkDir = Join-Path $env:TEMP "ClassicUsOfficialRoleAssets"
$RipperDir = Join-Path $WorkDir "AssetRipper"
$ExportDir = Join-Path $WorkDir "Exported"
$ZipPath = Join-Path $WorkDir "AssetRipper.zip"
$RipperUrl = "https://github.com/nowl-it/AssetRipperCLI/releases/download/v1.0.1/AssetRipper_win_x64.zip"

Write-Host ""
Write-Host "Classic Us Official Roles - local asset extractor"
Write-Host "Modern Among Us folder: $ModernContent"
Write-Host ""

if (-not (Test-Path (Join-Path $ModernContent "GameAssembly.dll"))) {
    throw "GameAssembly.dll was not found in '$ModernContent'. Pass the real Among Us Content folder, for example: .\Extract-OfficialAssets.ps1 -ModernContent 'C:\XboxGames\Among Us\Content'"
}

if (-not (Test-Path (Join-Path $ModernContent "Among Us_Data"))) {
    throw "Among Us_Data was not found in '$ModernContent'."
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function Find-AssetRipperExe {
    param([string]$Root)

    if (-not (Test-Path $Root)) { return $null }

    $preferred = @(
        "AssetRipper.GUI.Free.exe",
        "AssetRipper.exe"
    )

    foreach ($name in $preferred) {
        $hit = Get-ChildItem $Root -Recurse -File -Filter $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit }
    }

    return Get-ChildItem $Root -Recurse -File -Filter "*.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "AssetRipper*.exe" } |
        Select-Object -First 1
}

$RipperExe = Find-AssetRipperExe $RipperDir
if (-not $RipperExe) {
    Write-Host "Downloading AssetRipper CLI..."
    Invoke-WebRequest -Uri $RipperUrl -OutFile $ZipPath
    if (Test-Path $RipperDir) { Remove-Item $RipperDir -Recurse -Force }
    Expand-Archive -Path $ZipPath -DestinationPath $RipperDir -Force
    $RipperExe = Find-AssetRipperExe $RipperDir
}
if (-not $RipperExe) {
    Write-Host ""
    Write-Host "Files extracted from AssetRipper package:"
    Get-ChildItem $RipperDir -Recurse -File -ErrorAction SilentlyContinue |
        Select-Object -First 50 -ExpandProperty FullName |
        ForEach-Object { Write-Host "  $_" }
    throw "No AssetRipper executable was found after extraction."
}

Write-Host "Using AssetRipper executable:"
Write-Host "  $($RipperExe.FullName)"

if (Test-Path $ExportDir) { Remove-Item $ExportDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null

Write-Host "Extracting primary assets from your installed Among Us copy."
Write-Host "This can take a while and can temporarily use several GB of disk space."
# AssetRipper GUI.Free v1.0.1 uses 'primary' for raw/primary-content export.
# Nullable bool options such as --ignore-streaming-assets are intentionally omitted here:
# the Windows release parser treats a trailing 'false' as an extra positional argument.
$RipperArgs = @(
    "--cli",
    "--input", $ModernContent,
    "--output", $ExportDir,
    "--mode", "primary",
    "--script-content-level", "Level2"
)

& $RipperExe.FullName @RipperArgs
$RipperExit = $LASTEXITCODE

$AnyExport = Get-ChildItem $ExportDir -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($RipperExit -ne 0 -or -not $AnyExport) {
    throw "AssetRipper did not produce an export. Exit code: $RipperExit. Check the AssetRipper text above for the first real error."
}

$Terms = @(
    "scientist", "vitals",
    "engineer", "vent",
    "tracker", "track",
    "noisemaker", "noise", "alert",
    "detective", "interrogate", "case",
    "judge", "overrule",
    "guardianangel", "guardian", "protect",
    "spiritguide", "influencer",
    "shapeshifter", "shapeshift",
    "phantom", "vanish",
    "viper", "dissolve", "acid"
)

Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue | Remove-Item -Force
$Index = New-Object System.Collections.Generic.List[string]
$Counter = 0

$Files = Get-ChildItem $ExportDir -Recurse -File | Where-Object {
    $ext = $_.Extension.ToLowerInvariant()
    $ext -in @(".png", ".jpg", ".jpeg", ".wav", ".ogg")
}

foreach ($file in $Files) {
    $normalized = ($file.FullName.ToLowerInvariant() -replace "[ _\\/\-]", "")
    $matched = $false
    foreach ($term in $Terms) {
        if ($normalized.Contains(($term -replace "[ _\-]", ""))) {
            $matched = $true
            break
        }
    }
    if (-not $matched) { continue }

    $Counter++
    $safeName = "{0:D4}_{1}" -f $Counter, $file.Name
    $dest = Join-Path $OutputDir $safeName
    Copy-Item $file.FullName $dest -Force
    $Index.Add($safeName + [char]9 + $file.FullName)
}

$IndexPath = Join-Path $OutputDir "asset-index.txt"
$Index | Set-Content -Path $IndexPath -Encoding UTF8

Write-Host ""
Write-Host "Done. Copied $Counter candidate official role assets to:"
Write-Host "  $OutputDir"
Write-Host ""
Write-Host "The mod will score these files by role name + button/icon/ability wording and use the best matching sprite."
Write-Host "Nothing from your Among Us install is uploaded anywhere by this script."
