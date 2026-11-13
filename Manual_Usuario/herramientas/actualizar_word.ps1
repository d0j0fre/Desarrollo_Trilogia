<#
  Abre el .docx generado en Microsoft Word, actualiza el índice de contenido, el índice de
  figuras y todos los campos (números de figura y de página), guarda, y exporta un PDF
  para revisar. Así, al abrir el Word, el índice ya está al día.

  Uso: .\herramientas\actualizar_word.ps1 [-Docx salida\Manual.docx] [-SinPdf]
#>
param(
  [string]$Docx = (Join-Path (Split-Path $PSScriptRoot) 'salida\Manual_de_Usuario_Distribuidora_JJ.docx'),
  [switch]$SinPdf
)
$ErrorActionPreference = 'Stop'
$Docx = (Resolve-Path $Docx).Path
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
  $doc = $word.Documents.Open($Docx, $false, $false, $false)
  # Dos pasadas: la primera fija la numeración de figuras y la paginación,
  # la segunda deja los números de página del índice correctos.
  for ($i = 0; $i -lt 2; $i++) {
    $doc.Fields.Update() | Out-Null
    foreach ($t in $doc.TablesOfContents) { $t.Update() | Out-Null }
    foreach ($t in $doc.TablesOfFigures) { $t.Update() | Out-Null }
    $doc.Repaginate()
  }
  # Campos de encabezados y pies.
  foreach ($s in $doc.Sections) {
    foreach ($h in $s.Headers) { $h.Range.Fields.Update() | Out-Null }
    foreach ($f in $s.Footers) { $f.Range.Fields.Update() | Out-Null }
  }
  $doc.Save()
  "Índice: $($doc.TablesOfContents.Count) · Índice de figuras: $($doc.TablesOfFigures.Count) · Páginas: $($doc.ComputeStatistics(2))"
  if (-not $SinPdf) {
    $pdf = [IO.Path]::ChangeExtension($Docx, '.pdf')
    # 17 = PDF; CreateBookmarks 1 = marcadores desde los títulos.
    $doc.ExportAsFixedFormat($pdf, 17, $false, 0, 0, 1, 1, 0, $true, $true, 1)
    "PDF: $pdf"
  }
  $doc.Close($false)
}
finally {
  $word.Quit()
  [void][Runtime.InteropServices.Marshal]::ReleaseComObject($word)
}
