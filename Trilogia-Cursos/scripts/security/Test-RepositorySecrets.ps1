[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (git rev-parse --show-toplevel).Trim()
$allowedExtensions = @('.md', '.json', '.xml', '.yml', '.yaml', '.ps1', '.psm1', '.sh', '.sql', '.cs', '.cshtml', '.js', '.ts', '.config', '.props', '.targets', '.pubxml')
$issues = [System.Collections.Generic.List[object]]::new()
$placeholderCount = 0

function Test-ApprovedPlaceholder {
    param([string]$Value)

    $normalized = $Value.Trim().Trim('"').Trim("'").Trim()
    return $normalized -match '^(<[^>]+>|\$[A-Za-z_][A-Za-z0-9_]*|%[^%]+%|\$\{[^}]+\}|\*+)$'
}

function Add-Issue {
    param([string]$Path, [int]$Line, [string]$Type)

    $issues.Add([pscustomobject]@{ Path = $Path; Line = $Line; Type = $Type })
}

$bannedDemoPattern = '(?i)\b(12' + '34|admin' + '12' + '34?|nueva' + 'clave' + '123|token-de-' + 'recuperacion)\b'

$trackedFiles = @(git -C $repositoryRoot ls-files)
foreach ($relativePath in $trackedFiles) {
    $normalizedPath = $relativePath.Replace('\', '/')
    if ($normalizedPath -match '(^|/)(bin|obj|\.vs|wwwroot/lib)/') { continue }

    $extension = [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()
    if ($allowedExtensions -notcontains $extension) { continue }

    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { continue }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    if ($bytes -contains 0) { continue }

    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($fullPath)) {
        $lineNumber++
        $lowerLine = $line.ToLowerInvariant()

        if ($line -match $bannedDemoPattern) {
            Add-Issue $relativePath $lineNumber 'Credencial demo reutilizable'
            continue
        }

        if (($lowerLine.Contains('server=') -or $lowerLine.Contains('data source=')) -and ($lowerLine.Contains('password') -or $lowerLine.Contains('pwd'))) {
            if ($line -notmatch '(?i)(?:server|data source)=.*?(?:password|pwd)\s*=\s*([^;\s]+)') { continue }
            if (Test-ApprovedPlaceholder $matches[1]) { $placeholderCount++; continue }
            Add-Issue $relativePath $lineNumber 'Connection string con password'
            continue
        }

        if ($lowerLine -match '"(?:password|newpassword|token|apikey|clientsecret|contrasena|contrasenna)"') {
            if ($line -notmatch '(?i)"(?:password|newpassword|token|apikey|clientsecret|contrasena|contrasenna)"\s*:\s*"([^"]*)"') { continue }
            if (Test-ApprovedPlaceholder $matches[1]) { $placeholderCount++; continue }
            Add-Issue $relativePath $lineNumber 'Valor sensible en configuracion o ejemplo'
            continue
        }

        if ($lowerLine -match '(?:apikey|api[_-]?key|clientsecret|access[_-]?token|refresh[_-]?token)') {
            if ($line -notmatch '(?i)(?:apikey|api[_-]?key|clientsecret|access[_-]?token|refresh[_-]?token)\s*[=:]\s*["'']?([^\s"'',;]+)') { continue }
            if (Test-ApprovedPlaceholder $matches[1]) { $placeholderCount++; continue }
            Add-Issue $relativePath $lineNumber 'API key, token o secreto de Azure'
            continue
        }

        if ($lowerLine.Contains('smtp') -and ($lowerLine.Contains('password') -or $lowerLine.Contains('secret'))) {
            if ($line -notmatch '(?i)(?:smtp.*(?:password|secret)|(?:password|secret).*smtp)\s*[=:]\s*["'']?([^\s"'',;]+)') { continue }
            if (Test-ApprovedPlaceholder $matches[1]) { $placeholderCount++; continue }
            Add-Issue $relativePath $lineNumber 'Credencial SMTP'
            continue
        }

        if ($line.Contains('PRIVATE ' + 'KEY-----')) {
            Add-Issue $relativePath $lineNumber 'Clave privada'
        }
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Error ("{0}:{1} [{2}] [SECRETO DETECTADO]" -f $issue.Path, $issue.Line, $issue.Type)
    }
    exit 1
}

Write-Host ("Secret scan passed. Tracked files reviewed: {0}. Approved placeholders: {1}." -f $trackedFiles.Count, $placeholderCount)
