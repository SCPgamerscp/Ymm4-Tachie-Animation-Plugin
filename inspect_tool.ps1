$asm = [System.Reflection.Assembly]::LoadFrom('C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4\YukkuriMovieMaker.Plugin.dll')
$asm.GetExportedTypes() | Where-Object { $_.Name -like '*Tool*' } | ForEach-Object {
    Write-Host $_.FullName
    foreach ($m in $_.GetMembers()) {
        Write-Host "  $m"
    }
}
