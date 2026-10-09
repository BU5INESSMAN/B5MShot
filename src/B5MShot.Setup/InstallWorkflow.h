#pragma once

// The same function is exercised with mocks locally and with real Appx deployment on CI.
inline constexpr wchar_t InstallWorkflowScript[] = LR"ps(
function Invoke-B5MShotInstall {
    param([string]$PackagePath,[version]$ExpectedVersion,[scriptblock]$Migrate,
        [scriptblock]$Prepare={param($processId,$version) [B5MShot.Update.UpdateHandoff]::Prepare($processId,$version,$true)},
        [scriptblock]$ReleaseShellHosts={param($version) [B5MShot.Update.UpdateHandoff]::ReleaseShellHosts($version)},
        [scriptblock]$Launch={param($family,$version) [B5MShot.Update.UpdateHandoff]::ActivatePackage($family,$version)})
    $ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; $deployed=$false
    $stage='Проверка запущенного приложения'
    try {
        # Coordinate a normal exit; legacy idle builds leave their WPF message loop.
        # Never use ForceApplicationShutdown, Stop-Process or terminate Explorer.
        $running=@(Get-Process -Name B5MShot -ErrorAction SilentlyContinue)
        foreach($process in $running) {
            $state=& $Prepare $process.Id $ExpectedVersion
            Write-Host ('Update handoff PID '+$process.Id+': '+$state)
            if($state -in @('OtherSession','OtherUser','Current','Exited')){continue}
            if($state -eq 'Busy') {
                return [pscustomobject]@{Code=1618;Installed=$false;Message='В B5MShot открыт редактор или диалог. Сохраните снимок и закройте это окно, затем повторите установку. Выходить из трея не нужно.'}
            }
            throw ('Не удалось безопасно завершить предыдущую версию B5MShot: '+$state+' (PID '+$process.Id+'). Приложение не закрывалось принудительно.')
        }
        $stage='Освобождение компонента меню B5MShot'
        $released=& $ReleaseShellHosts $ExpectedVersion
        Write-Host ('Released B5MShot menu hosts: '+$released)
        $stage='Установка пакета Windows'; Write-Host $stage
        $current=Get-AppxPackage -Name BU5INESSMAN.B5MShot -ErrorAction Stop | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1
        if(!$current -or [version]$current.Version -lt $ExpectedVersion) {
            try { Add-AppxPackage -Path $PackagePath -ErrorAction Stop }
            catch {
                $details=($_ | Format-List * -Force | Out-String); Write-Host $details
                if($details -match '0x80073D02' -and (Get-Command Add-AppxPackage).Parameters.ContainsKey('DeferRegistrationWhenPackagesAreInUse')) {
                    # Explorer's extension can still hold a package even when all app windows are closed.
                    Add-AppxPackage -Path $PackagePath -DeferRegistrationWhenPackagesAreInUse -ErrorAction Stop
                    $deployed=$true
                    $current=Get-AppxPackage -Name BU5INESSMAN.B5MShot -ErrorAction Stop | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1
                    if(!$current -or [version]$current.Version -lt $ExpectedVersion) {
                        return [pscustomobject]@{Code=3010;Installed=$true;Message='Обновление подготовлено. Windows завершит его после освобождения файлов приложения. Закройте окна Проводника; если версия не сменилась, выйдите из учётной записи Windows и войдите снова. Приложения принудительно не закрывались.'}
                    }
                } else { throw }
            }
        }
        $deployed=$true
        $stage='Проверка установленной версии'
        $package=Get-AppxPackage -Name BU5INESSMAN.B5MShot -ErrorAction Stop | Sort-Object {[version]$_.Version} -Descending | Select-Object -First 1
        if(!$package -or [version]$package.Version -lt $ExpectedVersion){throw 'Windows не зарегистрировала требуемую версию пакета.'}
        $warnings=New-Object 'System.Collections.Generic.List[string]'
        try { & $Migrate $package } catch { Write-Host ($_ | Format-List * -Force | Out-String); $warnings.Add('Не удалось перенести автозапуск. Проверьте его в настройках приложения.') }
        try {
            Remove-Item -LiteralPath 'Registry::HKEY_CURRENT_USER\Software\Classes\SystemFileAssociations\image\shell\B5MShot.Upload' -Recurse -Force -ErrorAction SilentlyContinue
        } catch { Write-Host $_; $warnings.Add('Старая команда Проводника не удалена.') }
        $stage='Запуск обновлённого приложения'
        try {
            # Activation returns the real PID; verify its version before reporting success.
            if($package.PackageFamilyName -notmatch '^BU5INESSMAN\.B5MShot_[a-zA-Z0-9]+$'){throw 'Неизвестный идентификатор пакета.'}
            $started=& $Launch $package.PackageFamilyName $ExpectedVersion
            Write-Host ('Started updated B5MShot PID '+$started)
        } catch { throw }
        return [pscustomobject]@{Code=0;Installed=$true;NeedsAttention=($warnings.Count -gt 0);Message=('B5MShot '+$package.Version+' установлен.'+[Environment]::NewLine+($warnings -join [Environment]::NewLine))}
    } catch {
        $details=($_ | Format-List * -Force | Out-String); Write-Host $details
        $hex=[regex]::Match($details,'(?i)0x[0-9a-f]{8}').Value
        if(!$hex){$hex=('0x{0:X8}' -f $_.Exception.HResult)}
        $message=$_.Exception.Message
        if($message.Length -gt 1400){$message=$message.Substring(0,1400)}
        $prefix=if($deployed){'Пакет установлен или подготовлен, но не удалось подтвердить завершение.'}else{'Установка не завершена.'}
        return [pscustomobject]@{Code=1603;Installed=$deployed;Message=($prefix+[Environment]::NewLine+$stage+' · '+$hex+[Environment]::NewLine+$message)}
    }
}
)ps";
