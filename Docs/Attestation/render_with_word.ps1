$ErrorActionPreference = 'Stop'
$reportPath = 'D:\Evo Web Design\FarmGame\Docs\Attestation\My_Little_Farm_Attestation_Report_Expanded.docx'
$qaDirectory = 'C:\Users\esinencuP\.codex\visualizations\2026\10\12\01a1316d-41a6-7b00-8409-a5110ef7f494\attestation_work'
$pdfPath = Join-Path $qaDirectory 'report.pdf'
$wordApp = $null
$wordDocument = $null
try {
    $wordApp = New-Object -ComObject Word.Application
    $wordApp.Visible = $false
    $wordApp.DisplayAlerts = 0
    $wordDocument = $wordApp.Documents.Open($reportPath, $false, $true)
    $wordDocument.Repaginate()
    $wordDocument.ExportAsFixedFormat($pdfPath, 17)
    Write-Output ('Pages: ' + $wordDocument.ComputeStatistics(2))
    Write-Output ('PDF: ' + $pdfPath)
} finally {
    if ($null -ne $wordDocument) { $wordDocument.Close(0); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wordDocument) }
    if ($null -ne $wordApp) { $wordApp.Quit(0); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wordApp) }
}
