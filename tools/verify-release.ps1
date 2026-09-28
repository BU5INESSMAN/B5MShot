param([string]$Directory='release/stable-0.8.2')
$ErrorActionPreference='Stop'
$Directory=(Resolve-Path -LiteralPath $Directory).Path
$thumbprint='C5475B8C2639D3F402CDBCC2076105974C9CACA9'
foreach($line in Get-Content "$Directory/SHA256SUMS.txt"){
 if($line -notmatch '^([a-f0-9]{64})  (B5MShot(?:-Setup)?\.exe)$'){throw 'Unexpected checksum entry'}
 $path=Join-Path $Directory $Matches[2]
 if((Get-FileHash $path).Hash -ne $Matches[1]){throw 'Checksum mismatch'}
 $signature=Get-AuthenticodeSignature $path
 if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $thumbprint){throw 'Invalid signature'}
 if([Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -ne '0.8.2.0' -and [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -ne '0.8.2'){throw 'Unexpected file version'}
 Write-Output "PASS hash, signature and version: $(Split-Path $path -Leaf)"
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class B5MReleaseResources {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadLibraryExW(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr FindResourceW(IntPtr module,IntPtr name,IntPtr type);
 [DllImport("kernel32.dll")] public static extern uint SizeofResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] public static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] public static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
}
'@
$module=[B5MReleaseResources]::LoadLibraryExW("$Directory/B5MShot-Setup.exe",[IntPtr]::Zero,0x22)
if($module -eq [IntPtr]::Zero){throw 'Cannot read installer resources'}
try {
 $resource=[B5MReleaseResources]::FindResourceW($module,[IntPtr]201,[IntPtr]10)
 $size=[B5MReleaseResources]::SizeofResource($module,$resource)
 if($size -lt 1000000){throw 'Missing embedded package'}
 $pointer=[B5MReleaseResources]::LockResource([B5MReleaseResources]::LoadResource($module,$resource))
 $bytes=[byte[]]::new($size)
 [Runtime.InteropServices.Marshal]::Copy($pointer,$bytes,0,$bytes.Length)
 $stream=[IO.MemoryStream]::new($bytes,$false)
 $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read)
 try {
  $reader=[IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
  try {$manifest=[xml]$reader.ReadToEnd()} finally {$reader.Dispose()}
  if($manifest.Package.Identity.Version -ne '0.8.2.0' -or $manifest.Package.Identity.Publisher -ne 'CN=BU5INESSMAN'){throw 'Embedded package identity differs'}
  if($manifest.SelectSingleNode("//*[local-name()='ExcludedKey']").InnerText -ne 'HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run'){throw 'Missing installed autostart registry policy'}
  if(!$manifest.SelectSingleNode("//*[local-name()='Capability' and @Name='unvirtualizedResources']")){throw 'Missing registry access capability'}
  $embedded=$archive.GetEntry('B5MShot.exe').Open()
  try {$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($embedded))} finally {$embedded.Dispose()}
  if($hash -ne (Get-FileHash "$Directory/B5MShot.exe").Hash){throw 'Embedded app differs from portable app'}
  if(!$archive.GetEntry('B5MShot.ContextMenu.dll') -or !$archive.GetEntry('AppxSignature.p7x')){throw 'Missing Explorer extension or package signature'}
  Write-Output 'PASS embedded MSIX identity, signed package, Explorer extension and exact app hash'
 } finally {$archive.Dispose();$stream.Dispose()}
} finally {[void][B5MReleaseResources]::FreeLibrary($module)}
