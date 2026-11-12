<#
  Aplica, en orden, las migraciones 0023_* y 0200-0399 que falten en el ledger
  (dbo.SchemaMigrationHistory) y corre el .verify.sql de cada una.
  El SHA-256 se calcula sobre los bytes exactos del blob de git (no del archivo de trabajo).

  Ensayo en LocalDB:  .\aplicar_migraciones.ps1 -Pipe 'np:\.\pipe\LOCALDB#...\tsql\query' -SeguirAvisos -Saltar '^0269_'
  Azure (sesion de az login, cuenta administradora Entra del servidor SQL):
                      .\aplicar_migraciones.ps1 -Azure -SeguirAvisos -Saltar '^0269_'
  y despues una segunda corrida sin -Saltar: la 0269 exige la 0310, por eso va al final.

  -SeguirAvisos: si un .verify.sql falla, lo anota como AVISO y sigue (la migracion ya quedo aplicada).
#>
param([string]$Pipe, [switch]$Azure, [string]$Ref = 'HEAD', [string]$Log, [switch]$SeguirAvisos, [string]$Saltar = '^$')
$ErrorActionPreference = 'Stop'
$env:PATH = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;C:\Program Files\SqlCmd;$env:PATH"
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$dest = Join-Path $env:TEMP 'mig_azure'
New-Item -ItemType Directory -Force $dest | Out-Null
if ($Azure) { $con = @('-S','tcp:sql-trilogia-free-cr01.database.windows.net,1433','-d','DistribuidoraJJ_DB','--authentication-method','ActiveDirectoryAzCli','-l','120') }
else { $con = @('-S',$Pipe,'-d','DistribuidoraJJ_DB','-E') }
function Log($t){ $t; if($Log){ Add-Content -Encoding UTF8 $Log $t } }
$hechas = sqlcmd @con -h -1 -W -Q "SET NOCOUNT ON; SELECT MigrationId FROM dbo.SchemaMigrationHistory WHERE Status=N'Applied'"
if ($LASTEXITCODE -ne 0) { throw "No se pudo leer el ledger: $hechas" }
Push-Location $repo
$nombres = git ls-tree -r --name-only $Ref -- Trilogia-Cursos/database/migrations |
  Where-Object { $_ -match '/(0023_[^.]+|0[23]\d\d_[^.]+)\.sql$' } | Sort-Object
$n = 0
foreach ($ruta in $nombres) {
  $base = Split-Path $ruta -Leaf; $id = $base -replace '\.sql$',''
  if ($hechas -contains $id) { continue }
  if ($id -match $Saltar) { continue }
  $archivo = Join-Path $dest $base
  cmd /c "git show ${Ref}:$ruta > `"$archivo`""
  $sha = (Get-FileHash $archivo -Algorithm SHA256).Hash
  $t0 = Get-Date
  $out = sqlcmd @con -I -b -i $archivo -v MigrationSha256=$sha 2>&1
  if ($LASTEXITCODE -ne 0) { Log "FALLO $id"; ($out | Select-Object -Last 10) | ForEach-Object { Log "   $_" }; Pop-Location; exit 1 }
  $vr = $ruta -replace '\.sql$','.verify.sql'
  $hayV = [bool](git ls-tree --name-only $Ref -- $vr)
  $vtxt = 'sin verificacion'
  if ($hayV) {
    $verif = Join-Path $dest ($base -replace '\.sql$','.verify.sql')
    cmd /c "git show ${Ref}:$vr > `"$verif`""
    $v = sqlcmd @con -I -b -i $verif 2>&1
    if ($LASTEXITCODE -ne 0) { Log "AVISO VERIFICACION $id"; ($v | Select-Object -Last 3) | ForEach-Object { Log "   $_" }; if (-not $SeguirAvisos) { Pop-Location; exit 1 } }
    $vtxt = 'verificada'
  }
  $n++; Log ("OK {0}  {1}  ({2:N0} s)" -f $id, $vtxt, ((Get-Date)-$t0).TotalSeconds)
}
Pop-Location
Log "Aplicadas: $n"
