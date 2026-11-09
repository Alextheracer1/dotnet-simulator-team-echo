[CmdletBinding()]
param (
    [switch]$ForceSeparateWindows,
    [switch]$All
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
& (Join-Path $scriptDir "scripts\start-all.ps1") @PSBoundParameters
