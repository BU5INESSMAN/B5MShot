// Kept compatible with Windows PowerShell 5.1's C# compiler: also embedded in Setup.
#if NET8_0_OR_GREATER
#pragma warning disable 8600, 8602, 8618
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace B5MShot.Update
{
    public static class UpdateHandoff
    {
        public const string MessageName = "B5MShot.PrepareForUpdate.v1";
        public const int Ready = 0xB501;
        public const int Busy = 0xB502;

        public static string DescribeWindows(int processId)
        {
            var text = new StringBuilder();
            foreach (var window in GetWindows(processId))
                text.AppendLine(window.Class + " | " + window.Title + " | visible=" + window.Visible);
            return text.ToString();
        }

        // Never target another user's/session's process, an unknown executable or a newer build.
        public static string Prepare(int processId, Version incoming, bool replaceSameVersion)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                using (var self = Process.GetCurrentProcess())
                {
                    if (process.HasExited) return "Exited";
                    if (process.Id == self.Id || process.SessionId != self.SessionId) return "OtherSession";
                    IntPtr token;
                    if (!OpenProcessToken(process.Handle, 8, out token)) return "UnknownOwner";
                    try
                    {
                        using (var owner = new WindowsIdentity(token))
                        using (var me = WindowsIdentity.GetCurrent())
                            if (owner.User != me.User) return "OtherUser";
                    }
                    finally { CloseHandle(token); }
                    var info = process.MainModule.FileVersionInfo;
                    Version installed;
                    if (info.ProductName != "B5MShot" || info.OriginalFilename != "B5MShot.dll" ||
                        !Version.TryParse(info.FileVersion, out installed)) return "UnknownApp";
                    if (installed > incoming || (!replaceSameVersion && installed == incoming)) return "Current";
                    var windows = GetWindows(processId);
                    var main = windows.Find(w => w.Title == "B5MShot" && w.Class.StartsWith("HwndWrapper[", StringComparison.Ordinal));
                    if (main == null) return "Starting";
                    UIntPtr response;
                    if (SendMessageTimeout(main.Handle, RegisterWindowMessage(MessageName), IntPtr.Zero,
                        IntPtr.Zero, 2, 2000, out response) == IntPtr.Zero) return "Unresponsive";
                    if (response.ToUInt64() == Busy) return "Busy";
                    if (response.ToUInt64() != Ready)
                    {
                        // Pre-protocol releases: only the known, idle WPF main loop can be ended.
                        // Hidden/minimized editors count as work too; unknown windows fail closed.
                        if (installed < new Version(0, 6, 0, 0) || installed >= new Version(0, 8, 4, 0)) return "Unsupported";
                        windows = GetWindows(processId);
                        foreach (var window in windows)
                            if (window.Handle != main.Handle &&
                                (window.Visible || (window.Class.StartsWith("HwndWrapper[", StringComparison.Ordinal) &&
                                 window.Title.Length != 0 && window.Title != "MediaContextNotificationWindow"))) return "Busy";
                        uint ownerId;
                        var thread = GetWindowThreadProcessId(main.Handle, out ownerId);
                        if (ownerId != processId || thread == 0) return "Starting";
                        // WM_QUIT exits the message pump, not TerminateProcess/Stop-Process.
                        if (!PostThreadMessage(thread, 0x0012, UIntPtr.Zero, IntPtr.Zero)) return "Unresponsive";
                    }
                    return process.WaitForExit(5000) ? "Exited" : "Unresponsive";
                }
            }
            catch (ArgumentException) { return "Exited"; }
            catch (InvalidOperationException) { return "Unavailable"; }
            catch (Exception) { return "Unavailable"; }
        }

        private sealed class WindowInfo
        {
            public IntPtr Handle;
            public string Title;
            public string Class;
            public bool Visible;
        }
        private static List<WindowInfo> GetWindows(int processId)
        {
            var result = new List<WindowInfo>();
            if (!EnumWindows(delegate(IntPtr handle, IntPtr unused)
            {
                uint owner;
                GetWindowThreadProcessId(handle, out owner);
                if (owner == processId)
                {
                    var title = new StringBuilder(512);
                    var name = new StringBuilder(256);
                    GetWindowText(handle, title, title.Capacity);
                    GetClassName(handle, name, name.Capacity);
                    result.Add(new WindowInfo { Handle = handle, Title = title.ToString(), Class = name.ToString(), Visible = IsWindowVisible(handle) });
                }
                return true;
            }, IntPtr.Zero)) throw new InvalidOperationException("Cannot inspect application windows");
            return result;
        }
        private delegate bool EnumWindowProc(IntPtr handle, IntPtr param);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr param);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wParam, IntPtr lParam);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
