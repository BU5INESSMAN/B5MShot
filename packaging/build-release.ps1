param([Parameter(Mandatory)][string]$CertificateThumbprint)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$output=Join-Path $repoRoot 'release/stable-0.8.2'
$package=Join-Path $output 'package'
$payload=Join-Path $repoRoot 'src/B5MShot.Setup/payload'
New-Item -ItemType Directory -Force -Path $output,$package,$payload | Out-Null
function Check-Native { if($LASTEXITCODE -ne 0){throw "Native command failed: $LASTEXITCODE"} }
$vswhere="${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
$vsRoot=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vsRoot){throw 'MSVC build tools are required'}
$vcvars=Join-Path $vsRoot 'VC/Auxiliary/Build/vcvars64.bat'
$vcEnvironment=& cmd /c "`"$vcvars`" >nul && set"
Check-Native
foreach($entry in $vcEnvironment){if($entry -match '^([^=]+)=(.*)$'){[Environment]::SetEnvironmentVariable($Matches[1],$Matches[2],'Process')}}
$sdk=Join-Path $env:WindowsSdkDir ('bin/'+$env:WindowsSDKVersion.TrimEnd('\')+'/x64')
$signtool=Join-Path $sdk 'signtool.exe'
$makeappx=Join-Path $sdk 'makeappx.exe'
Push-Location $repoRoot
try {
 & dotnet publish src/B5MShot.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $package
 Check-Native
 Push-Location src/B5MShot.ContextMenu
 try {
  & rc /nologo /c65001 "/fo$output/ExplorerCommand.res" ExplorerCommand.rc; Check-Native
  & cl /nologo /utf-8 /std:c++17 /EHsc /MT /O2 /LD /DUNICODE /D_UNICODE ExplorerCommand.cpp "$output/ExplorerCommand.res" "/Fo$output/ExplorerCommand.obj" /link "/OUT:$package/B5MShot.ContextMenu.dll" /DEF:ExplorerCommand.def ole32.lib shell32.lib uuid.lib
  Check-Native
 } finally {Pop-Location}
 foreach($name in 'B5MShot.exe','B5MShot.ContextMenu.dll'){
  & $signtool sign /fd SHA256 /sha1 $CertificateThumbprint "$package/$name"; Check-Native
 }
 Copy-Item packaging/AppxManifest.xml $package
 Copy-Item packaging/Assets $package -Recurse -Force
 & $makeappx pack /d $package /p "$payload/B5MShot.msix" /o; Check-Native
 & $signtool sign /fd SHA256 /sha1 $CertificateThumbprint "$payload/B5MShot.msix"; Check-Native
 Export-Certificate -Cert "Cert:/CurrentUser/My/$CertificateThumbprint" -FilePath "$payload/B5MShot.cer" | Out-Null
 Push-Location src/B5MShot.Setup
 try {
  & rc /nologo /c65001 "/fo$output/Setup.res" Setup.rc; Check-Native
  & cl /nologo /utf-8 /std:c++17 /EHsc /MT /O2 /DUNICODE /D_UNICODE Setup.cpp "$output/Setup.res" "/Fo$output/Setup.obj" /link "/OUT:$output/B5MShot-Setup.exe" /SUBSYSTEM:WINDOWS /MANIFEST:NO crypt32.lib shell32.lib user32.lib
  Check-Native
 } finally {Pop-Location}
 & $signtool sign /fd SHA256 /sha1 $CertificateThumbprint "$output/B5MShot-Setup.exe"; Check-Native
 Copy-Item "$package/B5MShot.exe" $output
 foreach($name in 'B5MShot.exe','B5MShot-Setup.exe'){
  if((Get-AuthenticodeSignature "$output/$name").SignerCertificate.Thumbprint -ne $CertificateThumbprint){throw "Missing signature: $name"}
 }
 $hashes=foreach($name in 'B5MShot.exe','B5MShot-Setup.exe'){"$((Get-FileHash "$output/$name" -Algorithm SHA256).Hash.ToLowerInvariant())  $name"}
 $hashes | Set-Content "$output/SHA256SUMS.txt" -Encoding ascii
} finally {Pop-Location}
