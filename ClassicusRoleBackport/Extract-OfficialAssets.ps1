param(
    [string]$ModernContent = "C:\XboxGames\Among Us\Content"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Tab = [char]9

$PluginDir = $PSScriptRoot
$OutputDir = Join-Path $PluginDir "OfficialAssets"
$WorkDir = Join-Path $env:TEMP "ClassicUsExactOfficialRoleAssets"
$RipperDir = Join-Path $WorkDir "AssetRipper"
$ExportDir = Join-Path $WorkDir "UnityProject"
$ZipPath = Join-Path $WorkDir "AssetRipper.zip"
$RipperUrl = "https://github.com/nowl-it/AssetRipperCLI/releases/download/v1.0.1/AssetRipper_win_x64.zip"

Write-Host ""
Write-Host "Classic Us Official Roles - EXACT local asset extractor"
Write-Host "Modern Among Us folder: $ModernContent"
Write-Host ""
Write-Host "No filename scoring. No best-match selection."
Write-Host "Assets are resolved from serialized Unity GUID + fileID references."
Write-Host ""

if (-not (Test-Path (Join-Path $ModernContent "GameAssembly.dll"))) {
    throw "GameAssembly.dll was not found in '$ModernContent'."
}
if (-not (Test-Path (Join-Path $ModernContent "Among Us_Data"))) {
    throw "Among Us_Data was not found in '$ModernContent'."
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function Find-AssetRipperExe {
    param([string]$Root)
    if (-not (Test-Path $Root)) { return $null }

    foreach ($name in @("AssetRipper.GUI.Free.exe", "AssetRipper.exe")) {
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
    throw "No AssetRipper executable was found after extraction."
}

Write-Host "Using AssetRipper:"
Write-Host "  $($RipperExe.FullName)"

if (Test-Path $ExportDir) { Remove-Item $ExportDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $ExportDir | Out-Null

Write-Host ""
Write-Host "Exporting a Unity project so exact GUID/fileID references are preserved."
Write-Host "This can temporarily use several GB of disk space."

$RipperArgs = @(
    "--cli",
    "--input", $ModernContent,
    "--output", $ExportDir,
    "--mode", "unity",
    "--script-content-level", "Level1"
)

& $RipperExe.FullName @RipperArgs
$RipperExit = $LASTEXITCODE

$AssetsRoot = Get-ChildItem $ExportDir -Recurse -Directory -Filter "Assets" -ErrorAction SilentlyContinue |
    Sort-Object { $_.FullName.Length } |
    Select-Object -First 1

if ($RipperExit -ne 0 -or -not $AssetsRoot) {
    throw "AssetRipper did not produce a Unity project. Exit code: $RipperExit."
}

Write-Host ""
Write-Host "Building exact Unity GUID map..."

$GuidMap = @{}
Get-ChildItem $AssetsRoot.FullName -Recurse -File -Filter "*.meta" | ForEach-Object {
    $metaText = [IO.File]::ReadAllText($_.FullName)
    $m = [regex]::Match($metaText, '(?m)^guid:\s*([0-9a-fA-F]+)\s*$')
    if (-not $m.Success) { return }

    $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
    if (Test-Path $assetPath) {
        $GuidMap[$m.Groups[1].Value.ToLowerInvariant()] = $assetPath
    }
}

$BlockCache = @{}

function Get-YamlBlocks {
    param([string]$Path)
    if ($BlockCache.ContainsKey($Path)) { return $BlockCache[$Path] }

    $result = @()
    try {
        $text = [IO.File]::ReadAllText($Path)
        $rx = [regex]'(?ms)^--- !u!(?<class>\d+) &(?<id>-?\d+)\r?\n(?<body>.*?)(?=^--- !u!|\z)'
        foreach ($m in $rx.Matches($text)) {
            $result += [pscustomobject]@{
                ClassId = $m.Groups["class"].Value
                FileId = $m.Groups["id"].Value
                Body = $m.Groups["body"].Value
                Path = $Path
            }
        }
    } catch { }

    $BlockCache[$Path] = $result
    return $result
}

function Get-BlockById {
    param([string]$Path, [string]$FileId)
    foreach ($b in (Get-YamlBlocks $Path)) {
        if ($b.FileId -eq [string]$FileId) { return $b }
    }
    return $null
}

function Get-PPtrField {
    param([string]$Body, [string]$Field)
    $f = [regex]::Escape($Field)
    $rx = [regex]("(?m)^\s*" + $f + ":\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*(\d+))?\s*\}")
    $m = $rx.Match($Body)
    if (-not $m.Success) { return $null }

    return [pscustomobject]@{
        FileId = $m.Groups[1].Value
        Guid = $m.Groups[2].Value.ToLowerInvariant()
        Type = $m.Groups[3].Value
        Field = $Field
    }
}

function Get-AllPPtrFields {
    param([string]$Body)
    $items = @()
    $rx = [regex]'(?m)^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*):\s*\{fileID:\s*(?<id>-?\d+)(?:,\s*guid:\s*(?<guid>[0-9a-fA-F]+),\s*type:\s*(?<type>\d+))?\s*\}'
    foreach ($m in $rx.Matches($Body)) {
        $items += [pscustomobject]@{
            Field = $m.Groups["field"].Value
            FileId = $m.Groups["id"].Value
            Guid = $m.Groups["guid"].Value.ToLowerInvariant()
            Type = $m.Groups["type"].Value
        }
    }
    return $items
}

function Resolve-PPtr {
    param($Ref, [string]$CurrentFile)
    if (-not $Ref -or $Ref.FileId -eq "0") { return $null }

    $targetPath = $CurrentFile
    if ($Ref.Guid) {
        if (-not $GuidMap.ContainsKey($Ref.Guid)) { return $null }
        $targetPath = $GuidMap[$Ref.Guid]
    }

    $block = $null
    $ext = [IO.Path]::GetExtension($targetPath).ToLowerInvariant()
    if ($ext -in @(".prefab", ".asset", ".unity", ".controller", ".mat", ".spriteatlas")) {
        $block = Get-BlockById $targetPath $Ref.FileId
    }

    return [pscustomobject]@{
        Path = $targetPath
        FileId = $Ref.FileId
        Guid = $Ref.Guid
        Block = $block
    }
}

function Get-MetaGuid {
    param([string]$AssetPath)
    $meta = $AssetPath + ".meta"
    if (-not (Test-Path $meta)) { return $null }
    $text = [IO.File]::ReadAllText($meta)
    $m = [regex]::Match($text, '(?m)^guid:\s*([0-9a-fA-F]+)\s*$')
    if ($m.Success) { return $m.Groups[1].Value.ToLowerInvariant() }
    return $null
}

function Find-ScriptGuidExact {
    param([string[]]$ClassNames)

    foreach ($className in $ClassNames) {
        $hits = @(Get-ChildItem $AssetsRoot.FullName -Recurse -File -Filter ($className + ".cs") -ErrorAction SilentlyContinue)
        if ($hits.Count -eq 0) { continue }

        if ($hits.Count -ne 1) {
            throw "EXACT MODE: found $($hits.Count) copies of $className.cs. Refusing to guess."
        }

        $guid = Get-MetaGuid $hits[0].FullName
        if ($guid) {
            return [pscustomobject]@{
                ClassName = $className
                Guid = $guid
                ScriptPath = $hits[0].FullName
            }
        }
    }

    return $null
}

function Find-RoleComponentExact {
    param([string]$ScriptGuid)

    $needle = "guid: $ScriptGuid"
    $matches = @()
    $yamlFiles = Get-ChildItem $AssetsRoot.FullName -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension.ToLowerInvariant() -in @(".prefab", ".asset", ".unity") }

    foreach ($file in $yamlFiles) {
        $hit = Select-String -Path $file.FullName -SimpleMatch $needle -Quiet -ErrorAction SilentlyContinue
        if (-not $hit) { continue }

        foreach ($b in (Get-YamlBlocks $file.FullName)) {
            $rx = [regex]("(?m)^\s*m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*" + [regex]::Escape($ScriptGuid) + ",\s*type:\s*3\s*\}")
            if ($rx.IsMatch($b.Body)) { $matches += $b }
        }
    }

    $prefabs = @($matches | Where-Object { [IO.Path]::GetExtension($_.Path).ToLowerInvariant() -eq ".prefab" })
    if ($prefabs.Count -eq 1) { return $prefabs[0] }
    if ($matches.Count -eq 1) { return $matches[0] }
    if ($matches.Count -eq 0) { return $null }

    throw "EXACT MODE: role script GUID $ScriptGuid occurs in $($matches.Count) serialized objects. Refusing to choose one."
}

function Get-PixelsPerUnit {
    param([string]$ImagePath)
    $meta = $ImagePath + ".meta"
    if (-not (Test-Path $meta)) { return 100.0 }
    $text = [IO.File]::ReadAllText($meta)
    $m = [regex]::Match($text, '(?m)^\s*spritePixelsToUnits:\s*([0-9.]+)\s*$')
    if ($m.Success) {
        return [double]::Parse($m.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
    return 100.0
}

function Get-SpriteRect {
    param([string]$ImagePath, [string]$FileId)

    $meta = $ImagePath + ".meta"
    if (-not (Test-Path $meta)) { return $null }

    $text = [IO.File]::ReadAllText($meta)
    $marker = "internalID: $FileId"
    $idx = $text.IndexOf($marker, [StringComparison]::Ordinal)
    if ($idx -lt 0) { return $null }

    $start = $text.LastIndexOf("  - serializedVersion:", $idx, [StringComparison]::Ordinal)
    if ($start -lt 0) { $start = [Math]::Max(0, $idx - 2500) }
    $next = $text.IndexOf("  - serializedVersion:", $idx + $marker.Length, [StringComparison]::Ordinal)
    if ($next -lt 0) { $next = [Math]::Min($text.Length, $idx + 2500) }

    $chunk = $text.Substring($start, $next - $start)
    $rx = [regex]'(?ms)^\s*rect:\s*\r?\n\s*serializedVersion:\s*\d+\s*\r?\n\s*x:\s*([-0-9.]+)\s*\r?\n\s*y:\s*([-0-9.]+)\s*\r?\n\s*width:\s*([-0-9.]+)\s*\r?\n\s*height:\s*([-0-9.]+)'
    $m = $rx.Match($chunk)
    if (-not $m.Success) { return $null }

    return [pscustomobject]@{
        X = [int][double]::Parse($m.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
        Y = [int][double]::Parse($m.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture)
        W = [int][double]::Parse($m.Groups[3].Value, [Globalization.CultureInfo]::InvariantCulture)
        H = [int][double]::Parse($m.Groups[4].Value, [Globalization.CultureInfo]::InvariantCulture)
    }
}

try { Add-Type -AssemblyName System.Drawing -ErrorAction Stop } catch { }

$Manifest = New-Object System.Collections.Generic.List[string]
$ReferenceMap = New-Object System.Collections.Generic.List[string]
$Missing = New-Object System.Collections.Generic.List[string]

function Export-ExactSprite {
    param(
        [string]$Key,
        $SpriteRef,
        [string]$CurrentYaml,
        [string]$SourceDescription
    )

    $resolved = Resolve-PPtr $SpriteRef $CurrentYaml
    if (-not $resolved) {
        $Missing.Add($Key + $Tab + "UNRESOLVED_PPTR" + $Tab + $SourceDescription)
        return $false
    }

    $path = $resolved.Path
    $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
    if ($ext -notin @(".png", ".jpg", ".jpeg", ".bmp", ".tga")) {
        $Missing.Add($Key + $Tab + "NOT_IMAGE" + $Tab + $SourceDescription + $Tab + $path + $Tab + "fileID=" + $SpriteRef.FileId)
        return $false
    }

    $safe = ($Key -replace '[^A-Za-z0-9._-]', '_')
    $outName = $safe + ".png"
    $outPath = Join-Path $OutputDir $outName
    $ppu = Get-PixelsPerUnit $path
    $rect = Get-SpriteRect $path $SpriteRef.FileId

    if ($rect -and ("System.Drawing.Bitmap" -as [type])) {
        $src = $null
        $crop = $null
        try {
            $src = [System.Drawing.Bitmap]::FromFile($path)
            $top = $src.Height - $rect.Y - $rect.H

            if ($rect.X -lt 0 -or $top -lt 0 -or
                ($rect.X + $rect.W) -gt $src.Width -or
                ($top + $rect.H) -gt $src.Height) {
                throw "Sprite rectangle is outside the exported texture."
            }

            $r = New-Object System.Drawing.Rectangle($rect.X, $top, $rect.W, $rect.H)
            $crop = $src.Clone($r, $src.PixelFormat)
            $crop.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            if ($crop) { $crop.Dispose() }
            if ($src) { $src.Dispose() }
        }
    }
    else {
        Copy-Item $path $outPath -Force
    }

    $rel = [IO.Path]::GetFileName($outPath)
    $ppuText = $ppu.ToString("0.###", [Globalization.CultureInfo]::InvariantCulture)
    $Manifest.Add($Key + $Tab + $rel + $Tab + $ppuText + $Tab + $SourceDescription)
    return $true
}

function Record-DirectReferences {
    param([string]$RoleKey, $RoleBlock)

    foreach ($p in (Get-AllPPtrFields $RoleBlock.Body)) {
        if ($p.Field -eq "m_Script" -or $p.FileId -eq "0") { continue }

        $r = Resolve-PPtr $p $RoleBlock.Path
        if ($r) {
            $ReferenceMap.Add($RoleKey + $Tab + $p.Field + $Tab + $p.FileId + $Tab + $p.Guid + $Tab + $r.Path)
        }
        else {
            $ReferenceMap.Add($RoleKey + $Tab + $p.Field + $Tab + $p.FileId + $Tab + $p.Guid + $Tab + "<unresolved>")
        }

        if ($r -and [IO.Path]::GetExtension($r.Path).ToLowerInvariant() -in @(".png", ".jpg", ".jpeg", ".bmp", ".tga")) {
            [void](Export-ExactSprite ($RoleKey + ".field." + $p.Field) $p $RoleBlock.Path ($RoleKey + "." + $p.Field))
        }
    }
}

function Resolve-AbilitySpriteExact {
    param([string]$RoleKey, $RoleBlock)

    # Exact serialized chain:
    # RoleBehaviour.buttonManager -> AbilityButton.graphic -> SpriteRenderer.m_Sprite
    $buttonRef = Get-PPtrField $RoleBlock.Body "buttonManager"
    if (-not $buttonRef) {
        $Missing.Add($RoleKey + $Tab + "NO_buttonManager" + $Tab + $RoleBlock.Path)
        return $false
    }

    $button = Resolve-PPtr $buttonRef $RoleBlock.Path
    if (-not $button -or -not $button.Block) {
        $Missing.Add($RoleKey + $Tab + "buttonManager_UNRESOLVED" + $Tab + $RoleBlock.Path)
        return $false
    }

    $graphicRef = Get-PPtrField $button.Block.Body "graphic"
    if (-not $graphicRef) {
        $Missing.Add($RoleKey + $Tab + "NO_graphic" + $Tab + $button.Path)
        return $false
    }

    $graphic = Resolve-PPtr $graphicRef $button.Path
    if (-not $graphic -or -not $graphic.Block) {
        $Missing.Add($RoleKey + $Tab + "graphic_UNRESOLVED" + $Tab + $button.Path)
        return $false
    }

    $spriteRef = Get-PPtrField $graphic.Block.Body "m_Sprite"
    if (-not $spriteRef) {
        $Missing.Add($RoleKey + $Tab + "NO_m_Sprite" + $Tab + $graphic.Path)
        return $false
    }

    return Export-ExactSprite $RoleKey $spriteRef $graphic.Path ($RoleKey + ".buttonManager.graphic.m_Sprite")
}

$RoleSpecs = @(
    [pscustomobject]@{ Key="scientist";    Classes=@("ScientistRole") },
    [pscustomobject]@{ Key="engineer";     Classes=@("EngineerRole") },
    [pscustomobject]@{ Key="tracker";      Classes=@("TrackerRole") },
    [pscustomobject]@{ Key="noisemaker";   Classes=@("NoisemakerRole") },
    [pscustomobject]@{ Key="detective";    Classes=@("DetectiveRole") },
    [pscustomobject]@{ Key="judge";        Classes=@("JudgeRole") },
    [pscustomobject]@{ Key="guardian";     Classes=@("GuardianAngelRole") },
    [pscustomobject]@{ Key="influencer";   Classes=@("InfluencerRole","SpiritGuideRole") },
    [pscustomobject]@{ Key="shapeshifter"; Classes=@("ShapeshifterRole") },
    [pscustomobject]@{ Key="phantom";      Classes=@("PhantomRole") },
    [pscustomobject]@{ Key="viper";        Classes=@("ViperRole") }
)

Get-ChildItem $OutputDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host ""
Write-Host "Resolving exact role objects..."

foreach ($spec in $RoleSpecs) {
    Write-Host ("  " + $spec.Key + "...") -NoNewline

    $script = Find-ScriptGuidExact $spec.Classes
    if (-not $script) {
        $Missing.Add($spec.Key + $Tab + "ROLE_CLASS_NOT_FOUND" + $Tab + ($spec.Classes -join ","))
        Write-Host " class not found"
        continue
    }

    $roleBlock = Find-RoleComponentExact $script.Guid
    if (-not $roleBlock) {
        $Missing.Add($spec.Key + $Tab + "ROLE_OBJECT_NOT_FOUND" + $Tab + $script.ClassName)
        Write-Host " role object not found"
        continue
    }

    $ReferenceMap.Add($spec.Key + $Tab + "ROLE_OBJECT" + $Tab + $roleBlock.FileId + $Tab + $script.Guid + $Tab + $roleBlock.Path)
    Record-DirectReferences $spec.Key $roleBlock

    foreach ($field in @("RoleIconColor", "RoleIconSolid")) {
        $iconRef = Get-PPtrField $roleBlock.Body $field
        if ($iconRef) {
            [void](Export-ExactSprite ($spec.Key + "." + $field) $iconRef $roleBlock.Path ($spec.Key + "." + $field))
        }
    }

    $ok = Resolve-AbilitySpriteExact $spec.Key $roleBlock
    if ($ok) {
        Write-Host " exact ability sprite resolved"
    }
    else {
        Write-Host " exact role resolved; ability sprite chain was not serialized"
    }
}

$manifestPath = Join-Path $OutputDir "exact-assets.tsv"
$referencePath = Join-Path $OutputDir "exact-reference-map.tsv"
$missingPath = Join-Path $OutputDir "exact-missing.tsv"

$manifestHeader = "# key" + $Tab + "relative_png_path" + $Tab + "pixels_per_unit" + $Tab + "exact_serialized_source"
$referenceHeader = "# role" + $Tab + "field" + $Tab + "fileID" + $Tab + "guid" + $Tab + "resolved_asset"
$missingHeader = "# key" + $Tab + "problem" + $Tab + "source"

@($manifestHeader, "# GENERATED FROM SERIALIZED GUID/fileID REFERENCES. NO FUZZY MATCHING.") + $Manifest |
    Set-Content -Path $manifestPath -Encoding UTF8
@($referenceHeader) + $ReferenceMap |
    Set-Content -Path $referencePath -Encoding UTF8
@($missingHeader) + $Missing |
    Set-Content -Path $missingPath -Encoding UTF8

Write-Host ""
Write-Host "Exact extraction finished."
Write-Host "  Exact sprite manifest : $manifestPath"
Write-Host "  Reference audit map   : $referencePath"
Write-Host "  Unresolved exact refs : $missingPath"
Write-Host ""
Write-Host "Resolved exact sprite entries: $($Manifest.Count)"
Write-Host "Unresolved entries: $($Missing.Count)"
Write-Host ""
Write-Host "An unresolved asset stays unresolved. No substitute is selected."
Write-Host "Nothing from your Among Us installation is uploaded by this script."
