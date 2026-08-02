$logDir = 'C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4\user\log'
$logs = Get-ChildItem -Path $logDir -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 3

foreach ($l in $logs) {
    Write-Host "=== LOG FILE: $($l.Name) (Last Modified: $($l.LastWriteTime)) ==="
    $lines = Get-Content $l.FullName -Encoding UTF8
    Write-Host "Total Lines: $($lines.Count)"
    $lines | Select-Object -Last 100 | ForEach-Object { Write-Host $_ }
    Write-Host "`n--------------------------------------------------`n"
}
