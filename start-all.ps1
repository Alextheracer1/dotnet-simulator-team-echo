[CmdletBinding()]
param (
    [switch]$ForceSeparateWindows
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
& (Join-Path $scriptDir "scripts\start-all.ps1") @PSBoundParameters

