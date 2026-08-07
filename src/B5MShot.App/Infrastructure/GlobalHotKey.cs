using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using B5MShot.App.Models;

namespace B5MShot.App.Infrastructure;

public sealed class GlobalHotKey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int FirstHotKeyId = 0xB500;

    private readonly Dictionary<int, Action> _actions = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public void Initialize(Window owner)
    {
        if (_source is not null)
        {
            return;
        }

        _handle = new WindowInteropHelper(owner).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle)
            ?? throw new InvalidOperationException("Не удалось создать обработчик горячих клавиш.");
        _source.AddHook(WindowMessageHook);
    }

    public void Register(int slot, HotKeyGesture gesture, Action action)
    {
        if (_source is null)
        {
            throw new InvalidOperationException("Обработчик горячих клавиш не инициализирован.");
        }

        if (gesture.IsDisabled)
        {
            return;
        }

        var id = FirstHotKeyId + slot;
        if (!RegisterHotKey(_handle, id, gesture.Modifiers | ModNoRepeat, gesture.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Комбинация «{gesture.DisplayName}» уже занята.");
        }

        _actions[id] = action;
    }

    public void Clear()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(_handle, id);
        }

        _actions.Clear();
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Clear();
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _handle = IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
