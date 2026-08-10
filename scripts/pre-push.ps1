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

$git = Get-GitExecutable

$autoBump = (& $git config --get nano.autoversion 2>$null)
if ($autoBump -eq 'false') {
    Write-Host 'nano.autoversion=false; automatic version bump disabled.'
    exit 0
}

$refs = @($input | ForEach-Object { $_ })
if ($refs.Count -eq 0) {
    exit 0
}

$branchRefs = $refs | Where-Object { $_ -match '^refs/heads/\S+ \S+ refs/heads/' }
if (-not $branchRefs) {
    exit 0
}

$dirty = (& $git status --porcelain -- Directory.Build.props 2>$null)
if ($dirty) {
    Write-Error 'Directory.Build.props has uncommitted changes. Commit or stash them first, or disable auto versioning with: git config nano.autoversion false'
    exit 1
}

$part = (& $git config --get nano.autoversionPart 2>$null)
if (-not $part -or $part -notin @('major', 'minor', 'patch')) {
    $part = 'minor'
}

$bumpScript = Join-Path $PSScriptRoot 'bump-version.ps1'
& $bumpScript -Part $part -Commit
exit $LASTEXITCODE
