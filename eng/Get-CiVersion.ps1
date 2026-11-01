param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[1-9][0-9]*$')]
    [string] $RunNumber
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = Split-Path $PSScriptRoot -Parent
[xml] $build = Get-Content (Join-Path $repository 'Directory.Build.props') -Raw
$baseVersion = [string] $build.Project.PropertyGroup.Version[0]
if ($baseVersion -notmatch '^\d+\.\d+\.\d+-(?:[0-9A-Za-z-]+)(?:\.[0-9A-Za-z-]+)*$') {
    throw 'The base package version must be a prerelease SemVer without build metadata.'
}

# The workflow run number is stable on reruns and increases for new runs.
"$baseVersion.ci.$RunNumber"
