using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace B5MShot.App.Services;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "B5MShot";

    public void RepairExistingRegistration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        var previous = key?.GetValue(ValueName) as string;
        var command = CommandForRepair(previous, CurrentPackageFamilyName(), Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (command is not null && !string.Equals(previous, command, StringComparison.Ordinal))
            key!.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public static string? CommandForRepair(string? previous, string? packageFamilyName, string windowsDirectory)
    {
        // Merely running a portable/test copy must not hijack an installed app's startup entry.
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrEmpty(packageFamilyName)) return null;
        return BuildCommand(null, packageFamilyName, windowsDirectory);
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Reading autostart setting");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть настройки автозапуска Windows.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        key.SetValue(ValueName, BuildCommand(Environment.ProcessPath, CurrentPackageFamilyName(),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows)), RegistryValueKind.String);
    }

    public static string BuildCommand(string? executablePath, string? packageFamilyName, string windowsDirectory)
    {
        // Package family identity is stable across MSIX updates, unlike WindowsApps/version paths.
        if (!string.IsNullOrEmpty(packageFamilyName))
        {
            if (!packageFamilyName.StartsWith("BU5INESSMAN.B5MShot_", StringComparison.Ordinal)
                || packageFamilyName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
                throw new InvalidOperationException("Неизвестная установленная версия B5MShot.");
            return $"\"{Path.Combine(windowsDirectory, "explorer.exe")}\" \"shell:AppsFolder\\{packageFamilyName}!B5MShot\"";
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Не удалось определить путь к B5MShot.");
        }

        return $"\"{executablePath}\"";
    }

    private static string? CurrentPackageFamilyName()
    {
        var length = 0;
        var result = GetCurrentPackageFamilyName(ref length, null);
        if (result == 15700) return null; // APPMODEL_ERROR_NO_PACKAGE: portable executable.
        if (result != 122 || length <= 1) throw new InvalidOperationException("Не удалось определить установленный пакет B5MShot.");
        var family = new StringBuilder(length);
        result = GetCurrentPackageFamilyName(ref length, family);
        if (result != 0) throw new InvalidOperationException("Не удалось получить имя установленного пакета B5MShot.");
        return family.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder? packageFamilyName);
}
