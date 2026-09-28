param([string]$BackupDirectory = "$PSScriptRoot/../release/autostart-backups")
$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name BU5INESSMAN.B5MShot | Sort-Object { [version]$_.Version } -Descending | Select-Object -First 1
if (!$package -or [version]$package.Version -lt [version]'0.8.1.0') { throw 'Installed B5MShot 0.8.1 or newer was not found.' }
$executable = Join-Path $package.InstallLocation 'B5MShot.exe'
$signature = Get-AuthenticodeSignature -LiteralPath $executable
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne 'C5475B8C2639D3F402CDBCC2076105974C9CACA9') { throw 'Installed executable signature differs.' }
$keyPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($keyPath, $true)
try {
    $previous = $key.GetValue('B5MShot')
    if ([string]::IsNullOrWhiteSpace($previous)) { throw 'Autostart is not enabled; refusing to enable it implicitly.' }
    $explorer = Join-Path ([Environment]::GetFolderPath('Windows')) 'explorer.exe'
    $command = '"' + $explorer + '" "shell:AppsFolder\' + $package.PackageFamilyName + '!B5MShot"'
    $backupRoot = [IO.Path]::GetFullPath($BackupDirectory)
    [IO.Directory]::CreateDirectory($backupRoot) | Out-Null
    $backup = Join-Path $backupRoot ('B5MShot-Run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff') + '.json')
    @{ RegistryPath="HKCU\$keyPath"; Name='B5MShot'; Value=$previous; Kind=$key.GetValueKind('B5MShot').ToString() } | ConvertTo-Json | Set-Content -LiteralPath $backup -Encoding utf8
    $key.SetValue('B5MShot', $command, [Microsoft.Win32.RegistryValueKind]::String)
    if ($key.GetValue('B5MShot') -ne $command) { throw 'Registry verification failed.' }
    Write-Output "PASS: startup now activates installed package $($package.Version), independent of its versioned directory."
    Write-Output "Command: $command"
    Write-Output "Previous value saved: $backup"
} finally { if ($key) { $key.Dispose() } }
