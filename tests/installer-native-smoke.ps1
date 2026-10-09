param([string]$Directory='release/stable-0.8.7')
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted'){throw 'Native deployment test requires a disposable hosted runner.'}
if(Get-AppxPackage -Name BU5INESSMAN.B5MShot){throw 'Existing package detected; refusing to alter it'}
$testExe=(Resolve-Path -LiteralPath "$Directory/B5MShot-Setup-Test.exe").Path
$process=Start-Process -FilePath $testExe -PassThru -WindowStyle Hidden
if(!$process.WaitForExit(180000)){throw 'Native installer did not finish within three minutes'}
Get-ChildItem "$env:LOCALAPPDATA/B5MShot/logs/setup-*" | ForEach-Object {Write-Output $_.Name;Get-Content -LiteralPath $_.FullName}
if($process.ExitCode -ne 0){throw "Native installer failed: $($process.ExitCode)"}
$package=Get-AppxPackage -Name BU5INESSMAN.B5MShot
if(!$package -or $package.Version -ne '0.8.7.0'){throw 'Native installer did not register expected version'}
if(!(Test-Path 'Cert:/LocalMachine/TrustedPeople/C5475B8C2639D3F402CDBCC2076105974C9CACA9')){throw 'Successful installation lost certificate trust'}
Write-Output 'PASS: real native installer, certificate import, Windows PowerShell subprocess, package registration, result files and retained certificate trust.'
