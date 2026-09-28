$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText("$PSScriptRoot/../src/B5MShot.Setup/AutoStartMigration.h")
$match=[regex]::Match($source,'(?s)LR"ps\((.*?)\)ps";')
if(!$match.Success){throw 'Missing installer migration fragment'}
$productionKey='Software\Microsoft\Windows\CurrentVersion\Run'
$testKey='Software\B5MShot.Autostart.Tests\'+[Guid]::NewGuid().ToString('N')
if(([regex]::Matches($match.Groups[1].Value,[regex]::Escape($productionKey))).Count -ne 1){throw 'Unexpected registry accesses'}
$script=[scriptblock]::Create($match.Groups[1].Value.Replace($productionKey,$testKey))
$realBefore=(Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run' -Name B5MShot -ErrorAction SilentlyContinue).B5MShot
$key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($testKey)
$package=[pscustomobject]@{PackageFamilyName='BU5INESSMAN.B5MShot_mdepjvqy5n31g'}
$expected='"'+(Join-Path ([Environment]::GetFolderPath('Windows')) 'explorer.exe')+'" "shell:AppsFolder\'+$package.PackageFamilyName+'!B5MShot"'
try {
    $key.SetValue('UnrelatedApp','leave untouched')
    & $script
    if($null -ne $key.GetValue('B5MShot')){throw 'Disabled startup was enabled'}
    $key.SetValue('B5MShot','"C:\old 0.6.0\B5MShot.exe"')
    & $script
    if($key.GetValue('B5MShot') -ne $expected){throw 'Old portable entry not migrated'}
    & $script
    if($key.GetValue('B5MShot') -ne $expected){throw 'Migration is not idempotent'}
    $key.SetValue('B5MShot','')
    & $script
    if($key.GetValue('B5MShot') -ne ''){throw 'Empty entry was enabled'}
    $key.SetValue('B5MShot','old-command')
    $package.PackageFamilyName='untrusted" family'
    $rejected=$false
    try { & $script } catch { $rejected=$true }
    if(!$rejected -or $key.GetValue('B5MShot') -ne 'old-command'){throw 'Invalid identity was accepted'}
    if($key.GetValue('UnrelatedApp') -ne 'leave untouched'){throw 'Unrelated entry changed'}
    if((Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run' -Name B5MShot -ErrorAction SilentlyContinue).B5MShot -ne $realBefore){throw 'Real startup entry changed during test'}
    [xml]$manifest=Get-Content "$PSScriptRoot/../packaging/AppxManifest.xml"
    if($manifest.SelectSingleNode("//*[local-name()='ExcludedKey']").InnerText -ne "HKEY_CURRENT_USER\$productionKey"){throw 'Missing scoped Windows 11 registry exclusion'}
    if($manifest.SelectSingleNode("//*[local-name()='RegistryWriteVirtualization' and namespace-uri()='http://schemas.microsoft.com/appx/manifest/desktop/windows10/6']").InnerText -ne 'disabled'){throw 'Missing Windows 10 fallback'}
    if(!$manifest.SelectSingleNode("//*[local-name()='Capability' and @Name='unvirtualizedResources']")){throw 'Missing manifest capability'}
    Write-Output 'PASS: installer migration, disabled/empty preservation, idempotence, unrelated values, identity validation and MSIX registry policy; real startup entry unchanged.'
} finally {
    $key.Dispose()
    if($testKey -match '^Software\\B5MShot\.Autostart\.Tests\\[a-f0-9]{32}$'){
        [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($testKey,$false)
    }
}
