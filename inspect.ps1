$files = Get-ChildItem 'C:\YukkuriMovieMaker4-20231229T073048Z-001\YukkuriMovieMaker4\*.dll'
foreach ($f in $files) {
    try {
        $a = [System.Reflection.Assembly]::LoadFrom($f.FullName)
        $t = $a.GetType('YukkuriMovieMaker.Controls.FileSelectorAttribute')
        if ($t -ne $null) {
            Write-Host "Found FileSelectorAttribute in $($f.Name)"
            foreach ($c in $t.GetConstructors()) {
                foreach ($p in $c.GetParameters()) {
                    Write-Host "Param type: $($p.ParameterType.FullName)"
                }
            }
        }
    } catch {}
}
