[CmdletBinding()]
param(
    [string]$Remote,
    [string]$Branch,
    [switch]$SkipVersionBump
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

$git = Get-GitExecutable
$pushArgs = @('push')
if ($SkipVersionBump) {
    $pushArgs += '--no-verify'
}
if ($Remote) {
    $pushArgs += $Remote
}
if ($Branch) {
    $pushArgs += $Branch
}

& $git @pushArgs
exit $LASTEXITCODE
