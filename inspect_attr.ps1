$dlls = Get-ChildItem -Path 'C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4' -Filter YukkuriMovieMaker*.dll

foreach ($dll in $dlls) {
    try {
        $asm = [System.Reflection.Assembly]::LoadFrom($dll.FullName)
        $attrs = $asm.GetExportedTypes() | Where-Object { $_.Name -like '*Attribute*' }
        if ($attrs) {
            Write-Host "=== DLL: $($dll.Name) ==="
            foreach ($a in $attrs) {
                Write-Host "  $($a.Name)"
            }
        }
    } catch {}
}
