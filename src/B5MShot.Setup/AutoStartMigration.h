#pragma once

// Executed by the unpackaged installer after Add-AppxPackage succeeds.
// Keep this isolated fragment testable without installing a package or touching real startup entries.
inline constexpr wchar_t AutoStartMigrationScript[] = LR"ps(
$run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run',$true);
try {
    if($run -and -not [string]::IsNullOrWhiteSpace($run.GetValue('B5MShot'))) {
        $family=$package.PackageFamilyName;
        if($family -notmatch '^BU5INESSMAN\.B5MShot_[a-zA-Z0-9]+$'){throw 'Unexpected B5MShot package family'};
        $explorer=Join-Path ([Environment]::GetFolderPath('Windows')) 'explorer.exe';
        $command='"'+$explorer+'" "shell:AppsFolder\'+$family+'!B5MShot"';
        $run.SetValue('B5MShot',$command,[Microsoft.Win32.RegistryValueKind]::String);
        if($run.GetValue('B5MShot') -ne $command){throw 'Autostart migration verification failed'};
    };
} finally { if($run){$run.Dispose()}; };
)ps";
