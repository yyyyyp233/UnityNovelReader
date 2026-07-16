[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$OutputPath,
    [switch]$KeepBuildProject
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$packageJsonPath = Join-Path $repositoryRoot 'package.json'
$exporterSource = Join-Path $PSScriptRoot 'UnityPackageBuilder\UnityNovelReaderPackageExporter.cs'

if (-not (Test-Path -LiteralPath $packageJsonPath -PathType Leaf)) {
    throw "package.json not found: $packageJsonPath"
}

if (-not (Test-Path -LiteralPath $exporterSource -PathType Leaf)) {
    throw "Unity package exporter not found: $exporterSource"
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $hubRoot = 'C:\Program Files\Unity\Hub\Editor'
    $UnityPath = Get-ChildItem -LiteralPath $hubRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw 'Unity.exe was not found. Pass its full path with -UnityPath.'
}

$packageJson = Get-Content -Raw -LiteralPath $packageJsonPath | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($packageJson.version)) {
    throw 'package.json does not contain a version.'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot ("Releases~\UnityNovelReader-$($packageJson.version).unitypackage")
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$buildRoot = [System.IO.Path]::GetFullPath(
    (Join-Path ([System.IO.Path]::GetTempPath()) 'UnityNovelReader\unitypackage-build'))
$buildSession = [System.IO.Path]::GetFullPath(
    (Join-Path $buildRoot ([Guid]::NewGuid().ToString('N'))))
$safePrefix = $buildRoot.TrimEnd('\') + '\'
if (-not $buildSession.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe temporary build path: $buildSession"
}

$compileProject = Join-Path $buildSession 'CompileProject'
$exportProject = Join-Path $buildSession 'ExportProject'
$compileCreateLog = Join-Path $buildSession 'create-compile-project.log'
$compileLog = Join-Path $buildSession 'compile-editor-assembly.log'
$exportCreateLog = Join-Path $buildSession 'create-export-project.log'
$exportLog = Join-Path $buildSession 'export-package.log'
$buildSucceeded = $false
[System.IO.Directory]::CreateDirectory($buildSession) | Out-Null

function Invoke-Unity {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    $quotedArguments = foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            '"' + $argument.Replace('"', '\"') + '"'
        }
        else {
            $argument
        }
    }

    $process = Start-Process -FilePath $UnityPath `
        -ArgumentList $quotedArguments `
        -PassThru `
        -Wait `
        -WindowStyle Hidden

    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $LogPath) {
            Get-Content -Tail 180 -LiteralPath $LogPath | Write-Host
        }

        throw "Unity exited with code $($process.ExitCode). Log: $LogPath"
    }
}

function New-TemporaryUnityProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    Invoke-Unity -LogPath $LogPath -Arguments @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-createProject', $ProjectPath,
        '-logFile', $LogPath
    )
}

function Remove-EmbeddedCompilerPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )

    $bytes = [System.IO.File]::ReadAllBytes($AssemblyPath)
    $neutralPdbName = 'UnityNovelReader.Editor.pdb'
    $needle = [System.Text.Encoding]::ASCII.GetBytes($neutralPdbName)
    $matches = [System.Collections.Generic.List[int]]::new()

    for ($offset = 0; $offset -le $bytes.Length - $needle.Length; $offset++) {
        $matchesNeedle = $true
        for ($index = 0; $index -lt $needle.Length; $index++) {
            if ($bytes[$offset + $index] -ne $needle[$index]) {
                $matchesNeedle = $false
                break
            }
        }

        if ($matchesNeedle) {
            $matches.Add($offset)
        }
    }

    if ($matches.Count -ne 1) {
        throw "Expected one embedded PDB name in the compiled assembly, found $($matches.Count)."
    }

    $pdbNameOffset = $matches[0]
    $pathStart = $pdbNameOffset
    while ($pathStart -gt 0 -and
        $bytes[$pathStart - 1] -ge 32 -and
        $bytes[$pathStart - 1] -le 126) {
        $pathStart--
    }

    $embeddedPathLength = ($pdbNameOffset + $needle.Length) - $pathStart
    $embeddedPath = [System.Text.Encoding]::ASCII.GetString(
        $bytes,
        $pathStart,
        $embeddedPathLength)
    if ($embeddedPath -eq $neutralPdbName) {
        Write-Host 'Compiler debug path is already neutral.'
        return
    }

    if ($embeddedPath -notmatch '^(?:[A-Za-z]:[\\/]|/(?:Users|home)/)') {
        throw 'The embedded PDB path was not absolute and could not be sanitized safely.'
    }

    for ($index = 0; $index -lt $embeddedPathLength; $index++) {
        $bytes[$pathStart + $index] = 0
    }

    [System.Array]::Copy($needle, 0, $bytes, $pathStart, $needle.Length)
    [System.IO.File]::WriteAllBytes($AssemblyPath, $bytes)

    $sanitizedBytes = [System.IO.File]::ReadAllBytes($AssemblyPath)
    $binaryText = [System.Text.Encoding]::ASCII.GetString($sanitizedBytes)
    if ($binaryText -match '(?i)(?:[A-Za-z]:[\\/]|/(?:Users|home)/)[\x20-\x7E]{0,512}?\.pdb') {
        throw 'An absolute PDB path remains in the compiled assembly.'
    }

    Write-Host 'Removed the absolute compiler PDB path from the release assembly.'
}

try {
    # Phase 1: compile the open-source Editor assembly in an isolated Unity project.
    New-TemporaryUnityProject -ProjectPath $compileProject -LogPath $compileCreateLog

    $compileAssetRoot = Join-Path $compileProject 'Assets\UnityNovelReaderSource'
    $compileEditor = Join-Path $compileAssetRoot 'Editor'
    [System.IO.Directory]::CreateDirectory($compileAssetRoot) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Editor') `
        -Destination $compileEditor -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Editor.meta') `
        -Destination (Join-Path $compileAssetRoot 'Editor.meta') -Force

    Invoke-Unity -LogPath $compileLog -Arguments @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $compileProject,
        '-logFile', $compileLog
    )

    $compiledAssembly = Join-Path $compileProject 'Library\ScriptAssemblies\UnityNovelReader.Editor.dll'
    if (-not (Test-Path -LiteralPath $compiledAssembly -PathType Leaf)) {
        throw "Compiled Editor assembly not found: $compiledAssembly"
    }

    Remove-EmbeddedCompilerPath -AssemblyPath $compiledAssembly

    # Phase 2: export only the DLL from a separate clean Unity project.
    New-TemporaryUnityProject -ProjectPath $exportProject -LogPath $exportCreateLog

    Copy-Item -LiteralPath $compiledAssembly `
        -Destination (Join-Path $exportProject 'Assets\UnityNovelReader.Editor.dll') -Force
    $buildEditor = Join-Path $exportProject 'Assets\Editor'
    [System.IO.Directory]::CreateDirectory($buildEditor) | Out-Null
    Copy-Item -LiteralPath $exporterSource `
        -Destination (Join-Path $buildEditor 'UnityNovelReaderPackageExporter.cs') -Force

    $previousOutput = [Environment]::GetEnvironmentVariable(
        'UNITY_NOVEL_READER_PACKAGE_OUTPUT', 'Process')
    try {
        [Environment]::SetEnvironmentVariable(
            'UNITY_NOVEL_READER_PACKAGE_OUTPUT', $resolvedOutput, 'Process')
        Invoke-Unity -LogPath $exportLog -Arguments @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', $exportProject,
            '-executeMethod', 'UnityNovelReaderPackageExporter.Export',
            '-logFile', $exportLog
        )
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            'UNITY_NOVEL_READER_PACKAGE_OUTPUT', $previousOutput, 'Process')
    }

    if (-not (Test-Path -LiteralPath $resolvedOutput -PathType Leaf)) {
        throw "Unity package was not created: $resolvedOutput"
    }

    $buildSucceeded = $true
    $packageFile = Get-Item -LiteralPath $resolvedOutput
    $assemblyFile = Get-Item -LiteralPath $compiledAssembly
    Write-Host 'Minimal binary Unity package build complete.'
    Write-Host "Assembly: $($assemblyFile.Length) bytes"
    Write-Host "Output:   $($packageFile.FullName)"
    Write-Host "Package:  $($packageFile.Length) bytes"
}
finally {
    if ($buildSucceeded -and -not $KeepBuildProject) {
        $resolvedBuildSession = [System.IO.Path]::GetFullPath($buildSession)
        if (-not $resolvedBuildSession.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unsafe path: $resolvedBuildSession"
        }

        if (Test-Path -LiteralPath $resolvedBuildSession) {
            Remove-Item -LiteralPath $resolvedBuildSession -Recurse -Force
        }
    }
    elseif (-not $buildSucceeded) {
        Write-Host "Build projects retained for diagnostics: $buildSession"
    }
}
