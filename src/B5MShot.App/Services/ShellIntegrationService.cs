using Microsoft.Win32;

namespace B5MShot.App.Services;

public sealed class ShellIntegrationService
{
    private const string MenuKeyPath = @"Software\Classes\SystemFileAssociations\image\shell\B5MShot.Upload";

    public void EnsureRegistered()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("Не удалось определить путь к B5MShot.");
            }

            using var menuKey = Registry.CurrentUser.CreateSubKey(MenuKeyPath, writable: true)
                ?? throw new InvalidOperationException("Не удалось создать пункт контекстного меню Windows.");
            menuKey.SetValue(string.Empty, "Загрузить в B5MShot", RegistryValueKind.String);
            menuKey.SetValue("MUIVerb", "Загрузить в B5MShot", RegistryValueKind.String);
            menuKey.SetValue("Icon", $"\"{executablePath}\",0", RegistryValueKind.String);
            menuKey.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);

            using var commandKey = menuKey.CreateSubKey("command", writable: true)
                ?? throw new InvalidOperationException("Не удалось настроить команду B5MShot.");
            commandKey.SetValue(string.Empty, $"\"{executablePath}\" --upload \"%1\"", RegistryValueKind.String);
        }
        catch (Exception exception)
        {
            ErrorLogService.Write(exception, "Registering Explorer context menu");
        }
    }
}
