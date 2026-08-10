[CmdletBinding()]
param(
    [ValidateSet('major', 'minor', 'patch')]
    [string]$Part = 'minor',
    [string]$Version,
    [switch]$Commit
)

$ErrorActionPreference = 'Stop'

function Get-GitExecutable {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidates = @(
        'C:\Program Files\Git\cmd\git.exe',
        'C:\Program Files (x86)\Git\cmd\git.exe'
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw 'git executable not found. Install Git for Windows or add git to PATH.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$content = Get-Content -Raw -LiteralPath $propsPath

if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must be in major.minor.patch form: '$Version'"
    }
    $newVersion = $Version
}
else {
    $match = [regex]::Match($content, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
    if (-not $match.Success) {
        throw 'Cannot find <Version>major.minor.patch</Version> in Directory.Build.props.'
    }

    $major = [int]$match.Groups[1].Value
    $minor = [int]$match.Groups[2].Value
    $patch = [int]$match.Groups[3].Value
    switch ($Part) {
        'major' {
            $major++
            $minor = 0
            $patch = 0
        }
        'minor' {
            $minor++
            $patch = 0
        }
        'patch' {
            $patch++
        }
    }
    $newVersion = "$major.$minor.$patch"
}

$updated = $content -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$newVersion</Version>"
$updated = $updated -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newVersion.0</AssemblyVersion>"
$updated = $updated -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$newVersion.0</FileVersion>"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($propsPath, $updated, $utf8NoBom)
Write-Host "Version bumped to $newVersion"

if ($Commit) {
    $git = Get-GitExecutable
    & $git commit --only -m "chore: bump version to $newVersion" -- Directory.Build.props
    exit $LASTEXITCODE
}
