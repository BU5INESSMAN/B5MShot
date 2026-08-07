using System.Windows.Input;

namespace B5MShot.App.Models;

public sealed record HotKeyGesture(string StorageValue, string DisplayName, uint Modifiers, uint VirtualKey)
{
    public static HotKeyGesture Disabled { get; } = new("Disabled", "Отключено", 0, 0);

    public bool IsDisabled => VirtualKey == 0;

    public static HotKeyGesture Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return Disabled;
        }

        value = value.Trim() switch
        {
            "print-screen" => "PrintScreen",
            "ctrl-print-screen" => "Ctrl+PrintScreen",
            "alt-print-screen" => "Alt+PrintScreen",
            "ctrl-shift-s" => "Ctrl+Shift+S",
            "alt-shift-s" => "Alt+Shift+S",
            "ctrl-alt-s" => "Ctrl+Alt+S",
            var current => current
        };

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return Disabled;
        }

        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "shift" => ModifierKeys.Shift,
                "alt" => ModifierKeys.Alt,
                "win" or "windows" => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
        }

        var keyName = parts[^1].Equals("PrintScreen", StringComparison.OrdinalIgnoreCase)
            ? nameof(Key.Snapshot)
            : parts[^1];
        if (!Enum.TryParse<Key>(keyName, true, out var key))
        {
            return Disabled;
        }

        return FromKey(key, modifiers) ?? Disabled;
    }

    public static HotKeyGesture? FromKey(Key key, ModifierKeys modifiers)
    {
        if (IsModifierKey(key))
        {
            return null;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return null;
        }

        var keyName = key == Key.Snapshot ? "PrintScreen" : key.ToString();
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(keyName);
        var storage = string.Join('+', parts);
        return new HotKeyGesture(storage, string.Join(" + ", parts), (uint)modifiers, (uint)virtualKey);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;
}
