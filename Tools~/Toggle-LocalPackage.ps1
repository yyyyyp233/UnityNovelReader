[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Uninstall')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$packageId = 'com.unitynovelreader.editor'
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Split-Path -Parent $PSScriptRoot
}
$resolvedProject = [System.IO.Path]::GetFullPath($ProjectPath)
$resolvedPackage = [System.IO.Path]::GetFullPath($PackagePath)
$manifestPath = Join-Path $resolvedProject 'Packages\manifest.json'
$projectVersionPath = Join-Path $resolvedProject 'ProjectSettings\ProjectVersion.txt'
$packageJsonPath = Join-Path $resolvedPackage 'package.json'

if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
    throw "Not a Unity project: $resolvedProject"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Unity package manifest not found: $manifestPath"
}

if (-not (Test-Path -LiteralPath $packageJsonPath -PathType Leaf)) {
    throw "Unity Novel Reader package.json not found: $packageJsonPath"
}

$content = [System.IO.File]::ReadAllText($manifestPath)
$newline = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
$escapedId = [System.Text.RegularExpressions.Regex]::Escape($packageId)
$entryPattern = '(?m)^(?<indent>[ \t]*)"' + $escapedId + '"\s*:\s*"(?<value>[^"]*)"(?<comma>\s*,?)\s*$'
$updated = $content

if ($Action -eq 'Install') {
    $packageUri = 'file:' + ($resolvedPackage -replace '\\', '/')
    $existing = [System.Text.RegularExpressions.Regex]::Match($content, $entryPattern)
    if ($existing.Success) {
        $replacement = $existing.Groups['indent'].Value + '"' + $packageId + '": "' + $packageUri + '"' + $existing.Groups['comma'].Value
        $updated = $content.Substring(0, $existing.Index) + $replacement + $content.Substring($existing.Index + $existing.Length)
    }
    else {
        $dependencies = [System.Text.RegularExpressions.Regex]::Match($content, '"dependencies"\s*:\s*\{')
        if (-not $dependencies.Success) {
            throw 'The Unity manifest does not contain a dependencies object.'
        }

        $insertAt = $dependencies.Index + $dependencies.Length
        $remainder = $content.Substring($insertAt)
        $hasOtherDependencies = -not [System.Text.RegularExpressions.Regex]::IsMatch($remainder, '^\s*\}')
        $comma = if ($hasOtherDependencies) { ',' } else { '' }
        $entry = $newline + '    "' + $packageId + '": "' + $packageUri + '"' + $comma
        $updated = $content.Insert($insertAt, $entry)
    }
}
else {
    $existing = [System.Text.RegularExpressions.Regex]::Match($content, $entryPattern)
    if (-not $existing.Success) {
        Write-Host "Unity Novel Reader is not present in $manifestPath"
        exit 0
    }

    $lineStart = $existing.Index
    $lineEnd = $existing.Index + $existing.Length
    if ($lineEnd + $newline.Length -le $content.Length -and $content.Substring($lineEnd, $newline.Length) -eq $newline) {
        $lineEnd += $newline.Length
    }
    elseif ($lineStart -ge $newline.Length -and $content.Substring($lineStart - $newline.Length, $newline.Length) -eq $newline) {
        $lineStart -= $newline.Length
    }

    $updated = $content.Remove($lineStart, $lineEnd - $lineStart)
}

if ($updated -eq $content) {
    Write-Host "No manifest change was required."
    exit 0
}

try {
    $null = $updated | ConvertFrom-Json
}
catch {
    throw "Refusing to write an invalid manifest: $($_.Exception.Message)"
}

$backupDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'UnityNovelReader\manifest-backups'
[System.IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
$timestamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')
$backupPath = Join-Path $backupDirectory ("manifest-$timestamp.json")
[System.IO.File]::Copy($manifestPath, $backupPath, $true)
[System.IO.File]::WriteAllText($manifestPath, $updated, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "$Action complete."
Write-Host "Manifest: $manifestPath"
Write-Host "Backup:   $backupPath"
Write-Host 'Unity may also update Packages\packages-lock.json. Reading state remains outside the project.'
