$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted'){throw 'Upgrade tests require a disposable hosted runner'}
Add-Type -Path "$PSScriptRoot/../src/Shared/UpdateHandoff.cs"
function Close-TestApp {
    foreach($item in Get-Process B5MShot -ErrorAction SilentlyContinue) {
        $result=[B5MShot.Update.UpdateHandoff]::Prepare($item.Id,[version]'0.8.7.0',$true)
        if($result -ne 'Exited'){throw "Cannot close idle test app: $result"}
    }
}
Start-Sleep -Seconds 3
Close-TestApp
$current=Get-AppxPackage -Name BU5INESSMAN.B5MShot
if(!$current -or $current.Version -ne '0.8.7.0'){throw 'Expected clean-install test baseline'}
Remove-AppxPackage -Package $current.PackageFullName
& "$PSScriptRoot/installer-deployment.ps1" -Installer 'release/previous/B5MShot-Setup.exe'
$old=Get-AppxPackage -Name BU5INESSMAN.B5MShot
if($old.Version -ne '0.8.6.0'){throw 'Wrong upgrade baseline'}
$oldProcess=Start-Process -FilePath (Join-Path $old.InstallLocation 'B5MShot.exe') -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
if($oldProcess.HasExited){throw 'Old app was not running before update'}
# Hold the real Explorer menu COM object across the update, reproducing package-in-use failures.
Add-Type @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class MenuLockFixture {
    [DllImport("ole32.dll", PreserveSig=true)]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder name);
    public static IntPtr Create() {
        var clsid = new Guid("B541C9AE-75E1-4A38-87AC-091A6CEB7A55");
        var iid = new Guid("00000000-0000-0000-C000-000000000046");
        IntPtr instance;
        // Force the real surrogate; Activator may load the DLL into this test process.
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 4, ref iid, out instance));
        return instance;
    }
    public static bool IsOurHost(Process process) {
        uint length = 0;
        if (GetPackageFullName(process.Handle, ref length, null) != 122) return false;
        var name = new StringBuilder((int)length);
        return GetPackageFullName(process.Handle, ref length, name) == 0 &&
            name.ToString() == "BU5INESSMAN.B5MShot_0.8.6.0_x64__mdepjvqy5n31g";
    }
}
'@
$otherHosts=@(Get-Process dllhost -ErrorAction SilentlyContinue | Where-Object { ![MenuLockFixture]::IsOurHost($_) } | Select-Object -ExpandProperty Id)
$menu=[MenuLockFixture]::Create()
Start-Sleep -Seconds 1
$menuHosts=@(Get-Process dllhost -ErrorAction SilentlyContinue | Where-Object { [MenuLockFixture]::IsOurHost($_) })
if($menuHosts.Count -eq 0){throw 'Real menu surrogate was not created'}
Write-Output ([B5MShot.Update.UpdateHandoff]::DescribeWindows($oldProcess.Id))
$runKey='HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
Set-ItemProperty $runKey -Name B5MShot -Value ('"'+(Resolve-Path 'release/previous/B5MShot.exe').Path+'"')
$setup=Start-Process -FilePath (Resolve-Path 'release/stable-0.8.7/B5MShot-Setup-Test.exe') -WindowStyle Hidden -PassThru
if(!$setup.WaitForExit(180000) -or $setup.ExitCode -ne 0){throw 'Upgrade installer failed'}
if(!$oldProcess.HasExited){throw 'Old background process survived update'}
$installed=Get-AppxPackage -Name BU5INESSMAN.B5MShot
if($installed.Version -ne '0.8.7.0'){throw 'Installed version was not updated'}
foreach($hostId in $otherHosts){if(!(Get-Process -Id $hostId -ErrorAction SilentlyContinue)){throw 'Unrelated COM host was closed'}}
[void][Runtime.InteropServices.Marshal]::Release($menu)
Write-Output 'PASS: upgrade releases only the old B5MShot menu surrogate, retaining unrelated COM hosts.'
$command=(Get-ItemProperty $runKey).B5MShot
if($command -notmatch 'shell:AppsFolder\\BU5INESSMAN.B5MShot_[a-zA-Z0-9]+!B5MShot' -or $command -match 'previous') {throw 'Legacy startup path was not repaired'}
Write-Output 'PASS: real 0.8.6 -> 0.8.7 MSIX upgrade with old background app; stable autostart migration.'
Start-Sleep -Seconds 2
Close-TestApp
# Simulate boot ordering: a legacy portable copy wins the mutex before package startup.
$legacy=Start-Process -FilePath (Resolve-Path 'release/previous/B5MShot.exe') -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
if($legacy.HasExited){throw 'Legacy portable baseline failed to start'}
$new=Start-Process -FilePath (Join-Path $installed.InstallLocation 'B5MShot.exe') -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3
if(!$legacy.HasExited -or $new.HasExited){throw 'New version failed to take over legacy single-instance mutex'}
Write-Output 'PASS: new package takes over from old portable instance at startup.'
Close-TestApp
# Execute the exact stable startup command as Windows does at sign-in.
Start-Process -FilePath "$env:WINDIR/explorer.exe" -ArgumentList ('shell:AppsFolder\'+$installed.PackageFamilyName+'!B5MShot') -WindowStyle Hidden
Start-Sleep -Seconds 3
$started=@(Get-Process B5MShot -ErrorAction SilentlyContinue)
if($started.Count -ne 1 -or $started[0].Path -ne (Join-Path $installed.InstallLocation 'B5MShot.exe')){throw 'Startup activation resolved to the wrong copy'}
Write-Output 'PASS: sign-in activation starts exactly one installed 0.8.7 process.'
Close-TestApp
# Actual application's editor, not just a protocol mock, must veto shutdown.
Add-Type -AssemblyName System.Drawing
$picture=Join-Path $env:RUNNER_TEMP 'b5mshot-upgrade-fixture.png'
$bitmap=[Drawing.Bitmap]::new(80,60)
try{$bitmap.Save($picture,[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
$editor=Start-Process -FilePath (Join-Path $installed.InstallLocation 'B5MShot.exe') -ArgumentList ('--upload "'+$picture+'"') -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 3
    $state=[B5MShot.Update.UpdateHandoff]::Prepare($editor.Id,[version]'0.8.7.0',$true)
    if($state -ne 'Busy' -or $editor.HasExited){throw "Real editor not protected: $state"}
    Write-Output 'PASS: real new app refuses update shutdown with an unsaved editor.'
} finally {
    # Synthetic editor created in this disposable runner; no user data exists here.
    if(!$editor.HasExited){$editor.Kill();$editor.WaitForExit()}
}
