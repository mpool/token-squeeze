# TokenSqueeze auto-index hook script (PowerShell)
# Runs on SessionStart to ensure the current directory is indexed.

$ErrorActionPreference = "SilentlyContinue"

$PluginRoot = if ($env:CLAUDE_PLUGIN_ROOT) { $env:CLAUDE_PLUGIN_ROOT } else { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }

# Platform binary detection (PowerShell is typically Windows)
$Binary = Join-Path $PluginRoot "bin\win-x64\token-squeeze.exe"

if (-not (Test-Path $Binary)) {
    Write-Error "TokenSqueeze: binary not found at $Binary, run build.sh first"
    exit 0
}

# Read auto_reindex setting from settings.json
$SettingsFile = Join-Path $PluginRoot "settings.json"
$AutoReindex = $false
if (Test-Path $SettingsFile) {
    try {
        $Settings = Get-Content $SettingsFile -Raw | ConvertFrom-Json
        $AutoReindex = [bool]$Settings.auto_reindex
    } catch {
        $AutoReindex = $false
    }
}

$Cwd = Get-Location | Select-Object -ExpandProperty Path

if (-not $AutoReindex) {
    # Check if cwd is already indexed
    try {
        $ListOutput = & $Binary list 2>$null
        if ($ListOutput -match [regex]::Escape($Cwd)) {
            # Already indexed, exit silently
            exit 0
        }
    } catch {
        Write-Error "TokenSqueeze: auto-index failed"
        exit 0
    }
}

# Run index on cwd
try {
    $IndexOutput = & $Binary index $Cwd 2>$null
} catch {
    Write-Error "TokenSqueeze: auto-index failed"
    exit 0
}

# Parse JSON output for summary
try {
    $IndexResult = $IndexOutput | ConvertFrom-Json
    $FilesIndexed = if ($IndexResult.filesIndexed) { $IndexResult.filesIndexed } else { 0 }
    $FilesUpdated = if ($IndexResult.filesUpdated) { $IndexResult.filesUpdated } else { 0 }

    if ($FilesIndexed -eq 0 -and $FilesUpdated -eq 0) {
        Write-Output "TokenSqueeze: index up to date"
    } else {
        Write-Output "TokenSqueeze: indexed $FilesIndexed files ($FilesUpdated updated)"
    }
} catch {
    Write-Output "TokenSqueeze: indexed (unable to parse details)"
}
