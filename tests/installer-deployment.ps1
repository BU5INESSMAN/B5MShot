param([string]$Installer='release/installer-probe/B5MShot-Setup.exe')
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Real deployment test is restricted to disposable GitHub runners.'}
$packageBefore=Get-AppxPackage -Name BU5INESSMAN.B5MShot
if($packageBefore){throw 'Runner is not clean; refusing to modify an existing installation.'}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PayloadReader {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr LoadLibraryExW(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll")] static extern IntPtr FindResourceW(IntPtr module,IntPtr name,IntPtr type);
 [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
 public static byte[] Read(string path,int id){
  var module=LoadLibraryExW(path,IntPtr.Zero,0x22); if(module==IntPtr.Zero)throw new Exception("Cannot read installer");
  try{var resource=FindResourceW(module,(IntPtr)id,(IntPtr)10);var size=SizeofResource(module,resource);if(size==0)throw new Exception("Missing resource");
   var bytes=new byte[size];Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length);return bytes;
  }finally{FreeLibrary(module);}
 }
}
'@
$installerPath=(Resolve-Path -LiteralPath $Installer).Path
$stage=Join-Path $env:RUNNER_TEMP ('b5mshot-install-probe-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
$msix=Join-Path $stage 'B5MShot.msix';$cer=Join-Path $stage 'B5MShot.cer'
[IO.File]::WriteAllBytes($msix,[PayloadReader]::Read($installerPath,201))
[IO.File]::WriteAllBytes($cer,[PayloadReader]::Read($installerPath,202))
Import-Certificate -FilePath $cer -CertStoreLocation Cert:/LocalMachine/TrustedPeople | Out-Null
try {
    Add-AppxPackage -Path $msix -ErrorAction Stop
    $package=Get-AppxPackage -Name BU5INESSMAN.B5MShot
    if(!$package){throw 'Appx registration did not produce installed package'}
    Write-Output "PASS: real clean deployment $($package.PackageFullName) in Windows PowerShell $($PSVersionTable.PSVersion)"
} catch {
    Write-Output ($_ | Format-List * -Force | Out-String)
    Write-Output ('Exception HRESULT: 0x{0:X8}' -f $_.Exception.HResult)
    throw
}
