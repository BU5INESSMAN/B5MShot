$ErrorActionPreference='Stop'
$header=[IO.File]::ReadAllText("$PSScriptRoot/../src/B5MShot.Setup/InstallWorkflow.h")
$body=[regex]::Match($header,'(?s)LR"ps\((.*?)\)ps";').Groups[1].Value
if(!$body){throw 'Missing installation workflow'}
. ([scriptblock]::Create($body))
$script:mode='';$script:added=$false;$script:deferred=$false;$script:launched=$false;$script:launchTarget=''
function Get-Process { param($Name,$ErrorAction) if($script:mode -in @('running','idle','hung','other-session')){[pscustomobject]@{Id=123}} }
function Start-Sleep { param($Milliseconds) }
function Get-AppxPackage {
    param($Name,$ErrorAction)
    if($script:mode -eq 'verify-fails' -and $script:added){return}
    if($script:mode -eq 'newer'){$version='0.9.0.0'}
    elseif($script:added -and (!$script:deferred -or $script:mode -eq 'defer-completes')){$version='0.8.4.0'}
    elseif($script:mode -in @('update','defer','defer-completes')){$version='0.8.1.0'}
    else{return}
    [pscustomobject]@{Version=$version;PackageFamilyName='BU5INESSMAN.B5MShot_mdepjvqy5n31g'}
}
function Add-AppxPackage {
    param($Path,[switch]$DeferRegistrationWhenPackagesAreInUse,$ErrorAction)
    if($script:mode -eq 'deploy-fails'){throw 'Deployment failed with HRESULT: 0x80073CF6'}
    if($script:mode -in @('defer','defer-completes') -and !$DeferRegistrationWhenPackagesAreInUse){throw 'Resources in use 0x80073D02'}
    $script:added=$true; $script:deferred=[bool]$DeferRegistrationWhenPackagesAreInUse
}
function Remove-Item { param($LiteralPath,[switch]$Recurse,[switch]$Force,$ErrorAction) }
function Start-TestPackage {
    param($family,$version)
    if($script:mode -eq 'launch-fails'){throw 'Access denied on automatic app launch'}
    $script:launched=$true; $script:launchTarget=$family
    return 456
}
foreach($case in @('clean','update','running','idle','hung','other-session','deploy-fails','migration-fails','launch-fails','defer','defer-completes','newer','verify-fails')) {
    $script:mode=$case;$script:added=$false;$script:deferred=$false;$script:launched=$false
    $result=Invoke-B5MShotInstall -PackagePath 'fixture.msix' -ExpectedVersion '0.8.4.0' -Migrate {param($package) if($script:mode -eq 'migration-fails'){throw 'Access denied on startup migration'}} -Prepare {
        param($processId,$version)
        switch($script:mode) { running {'Busy'} idle {'Exited'} hung {'Unresponsive'} other-session {'OtherSession'} default {throw 'Unexpected process'} }
    } -ReleaseShellHosts {param($version) return 1} -Launch ${function:Start-TestPackage}
    if(@($result).Count -ne 1){throw "Workflow leaked pipeline objects: $case"}
    switch($case) {
        running { if($result.Code -ne 1618 -or $script:added -or $result.Installed){throw 'Running editor not protected'} }
        hung { if($result.Code -ne 1603 -or $script:added){throw 'Unresponsive app was terminated'} }
        deploy-fails { if($result.Code -ne 1603 -or $result.Installed -or $result.Message -notmatch '0x80073CF6' -or $result.Message -match '1618'){throw 'Deployment error lost its real cause'} }
        migration-fails { if($result.Code -ne 0 -or !$result.Installed -or $result.Message.Length -lt 80){throw 'Migration warning turned successful install into failure'} }
        launch-fails { if($result.Code -ne 1603 -or !$result.Installed -or $result.Message -notmatch 'Access denied'){throw 'Failed launch was falsely reported as success'} }
        defer { if($result.Code -ne 3010 -or !$result.Installed -or !$script:deferred -or $script:launched){throw 'Deferred package reported as fully installed'} }
        verify-fails { if($result.Code -ne 1603 -or !$result.Installed){throw 'Deployment uncertainty would remove certificate trust'} }
        default { if($result.Code -ne 0 -or !$result.Installed -or !$script:launched -or $script:launchTarget -notmatch '^BU5INESSMAN\.B5MShot_'){throw "Installation state failed: $case"} }
    }
    if($case -eq 'newer' -and $script:added){throw 'Newer package was downgraded'}
    Write-Output "PASS: $case (code $($result.Code), installed/staged=$($result.Installed))"
}
