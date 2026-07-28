[CmdletBinding()]
param(
    [string]$ServerInstance,
    [string]$Database,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$migrationPath = Join-Path $PSScriptRoot '..\..\database\migrations\0012_inventory_combos_transformations_intelligence.sql'
$migrationPath = (Resolve-Path -LiteralPath $migrationPath).Path
$migrationSha256 = (Get-FileHash -LiteralPath $migrationPath -Algorithm SHA256).Hash.ToUpperInvariant()

if ($migrationSha256 -notmatch '^[0-9A-F]{64}$') {
    throw 'No fue posible calcular un SHA-256 hexadecimal válido para la migración 0012.'
}

if ($DryRun) {
    Write-Output "SHA-256 de 0012 validado: $migrationSha256"
    return
}

if ([string]::IsNullOrWhiteSpace($ServerInstance) -or [string]::IsNullOrWhiteSpace($Database)) {
    throw 'ServerInstance y Database son obligatorios salvo en modo -DryRun.'
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($null -eq $sqlcmd) {
    throw 'sqlcmd no está disponible en PATH.'
}

$sqlcmdArguments = @(
    '-S', $ServerInstance,
    '-d', $Database,
    '-I',
    '-b',
    '-i', $migrationPath,
    '-v', 'MigrationSha256', '=', $migrationSha256
)

& $sqlcmd.Path @sqlcmdArguments

if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd finalizó con código $LASTEXITCODE al ejecutar 0012."
}

Write-Output 'Migración 0012 ejecutada correctamente.'
