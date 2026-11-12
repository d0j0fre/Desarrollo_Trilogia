<#
  Compila la app móvil para las capturas del manual (modo profile: rápido en el emulador)
  apuntando a la API local, y la instala en el emulador.

  Uso: .\compilar_app.ps1 -Repo C:\dev\Desarrollo_Trilogia\.claude\worktrees\<worktree>\Trilogia-Cursos

  La app solo acepta HTTPS. Para hablar con la API local (http://10.0.2.2:7171) se copia
  temporalmente una política de red que permite HTTP solo hacia 10.0.2.2, en las carpetas
  debug/ y profile/ de Android. Se borra al terminar: nunca se sube al repositorio.
#>
param(
  [Parameter(Mandatory)][string]$Repo,
  [string]$Api = 'http://10.0.2.2:7171/'
)
$ErrorActionPreference = 'Stop'
$movil = Join-Path $Repo 'Proyecto_Movil'
$politica = Join-Path $PSScriptRoot 'network_security_config.debug.xml'
$destinos = @('debug', 'profile') | ForEach-Object { Join-Path $movil "android\app\src\$_\res\xml" }
$creados = @()
try {
  foreach ($d in $destinos) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Force $d | Out-Null; $creados += $d }
    Copy-Item $politica (Join-Path $d 'network_security_config.xml') -Force
  }
  Push-Location $movil
  # Flutter escribe avisos en stderr; en PowerShell 5.1 eso no debe cortar el script.
  $ErrorActionPreference = 'Continue'
  flutter pub get | Out-Null
  flutter build apk --profile "--dart-define=API_BASE_URL=$Api"
  if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
  Pop-Location
  $apk = Join-Path $movil 'build\app\outputs\flutter-apk\app-profile.apk'
  Copy-Item $apk (Join-Path $PSScriptRoot 'app-manual.apk') -Force
  $adb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
  & $adb install -r (Join-Path $PSScriptRoot 'app-manual.apk')
}
finally {
  foreach ($d in $destinos) { Remove-Item (Join-Path $d 'network_security_config.xml') -ErrorAction SilentlyContinue }
  foreach ($d in $creados) { Remove-Item (Split-Path $d) -Recurse -Force -ErrorAction SilentlyContinue }
}
