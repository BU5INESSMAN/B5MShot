param([string]$Fixture='tests/HandoffFixture/bin/Release/net8.0-windows/win-x64/publish/B5MShot.exe')
$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot/../src/Shared/UpdateHandoff.cs"
$path=(Resolve-Path -LiteralPath $Fixture).Path
foreach($mode in @('idle','editor','protocol','busy','same-version','newer-version')) {
    $process=Start-Process -FilePath $path -ArgumentList $mode -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Milliseconds 1000
        $incoming=if($mode -eq 'same-version'){[version]'0.6.0.0'}elseif($mode -eq 'newer-version'){[version]'0.5.0.0'}else{[version]'0.8.4.0'}
        $result=[B5MShot.Update.UpdateHandoff]::Prepare($process.Id,$incoming,$false)
        $expected=if($mode -in @('editor','busy')){'Busy'}elseif($mode -in @('same-version','newer-version')){'Current'}else{'Exited'}
        if($result -ne $expected){throw "Handoff $mode expected $expected but got $result : $([B5MShot.Update.UpdateHandoff]::DescribeWindows($process.Id))"}
        if($expected -eq 'Exited' -and !$process.HasExited){throw 'Handoff did not release process'}
        if($expected -ne 'Exited' -and $process.HasExited){throw 'Protected process was closed'}
        Write-Output "PASS real WPF process: $mode -> $result"
    } finally {
        # Only the synthetic process created above, never a user's B5MShot instance.
        if(!$process.HasExited){$process.Kill();$process.WaitForExit()}
        $process.Dispose()
    }
}
