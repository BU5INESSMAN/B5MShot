// Kept compatible with Windows PowerShell 5.1's C# compiler: also embedded in Setup.
#if NET8_0_OR_GREATER
#pragma warning disable 8600, 8602, 8618, 8625
#endif
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public const string UpdatePromptTitle = "\u0414\u043e\u0441\u0442\u0443\u043f\u043d\u043e \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0435 B5MShot";

        public static bool IsOurShellHost(string executable, string packageName, Version incoming)
        {
            var parts = packageName.Split('_');
            Version version;
            return (string.Equals(executable, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dllhost.exe"), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(executable, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "dllhost.exe"), StringComparison.OrdinalIgnoreCase)) &&
                parts.Length == 5 && parts[0] == "BU5INESSMAN.B5MShot" && parts[3] == "" && parts[4] == "mdepjvqy5n31g" &&
                Version.TryParse(parts[1], out version) && version <= incoming;
        }

        public static int ReleaseShellHosts(Version incoming)
        {
            var released = 0;
            using (var self = Process.GetCurrentProcess())
            foreach (var process in Process.GetProcessesByName("dllhost"))
            using (process)
            {
                var selected = false;
                try
                {
                if (process.HasExited || process.SessionId != self.SessionId) continue;
                IntPtr token;
                if (!OpenProcessToken(process.Handle, 8, out token)) continue;
                try
                {
                    using (var owner = new WindowsIdentity(token))
                    using (var me = WindowsIdentity.GetCurrent())
                        if (owner.User != me.User) continue;
                }
                finally { CloseHandle(token); }
                uint length = 0;
                if (GetPackageFullName(process.Handle, ref length, null) != 122) continue;
                var package = new StringBuilder((int)length);
                if (GetPackageFullName(process.Handle, ref length, package) != 0 ||
                    !IsOurShellHost(process.MainModule.FileName, package.ToString(), incoming)) continue;
                // Only our isolated menu COM server: it holds no screenshot/editor state.
                // Explorer and other packages' COM servers are never targeted.
                if (GetWindows(process.Id).Exists(w => w.Visible)) continue;
                selected = true;
                process.Kill();
                if (!process.WaitForExit(5000)) throw new InvalidOperationException("B5MShot menu component did not release its package.");
                released++;
                }
                catch (Win32Exception) { if (selected) throw; }
                catch (InvalidOperationException) { if (selected) throw; }
            }
            return released;
        }

        public static int ActivatePackage(string family, Version expected)
        {
            if (family != "BU5INESSMAN.B5MShot_mdepjvqy5n31g") throw new ArgumentException("Unexpected package family.");
            var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
            if (type == null) throw new InvalidOperationException("Windows activation manager is unavailable.");
            var manager = (IApplicationActivationManager)Activator.CreateInstance(type);
            if (manager == null) throw new InvalidOperationException("Windows activation manager is unavailable.");
            uint id;
            try { Marshal.ThrowExceptionForHR(manager.ActivateApplication(family + "!B5MShot", "", 2, out id)); }
            finally { Marshal.ReleaseComObject(manager); }
            System.Threading.Thread.Sleep(1000);
            using (var process = Process.GetProcessById((int)id))
            {
                Version actual;
                if (process.HasExited || !Version.TryParse(process.MainModule.FileVersionInfo.FileVersion, out actual) || actual < expected)
                    throw new InvalidOperationException("Windows did not start the updated B5MShot version.");
            }
            return (int)id;
        }

        [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
                [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        }

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
                            if (window.Handle != main.Handle && !IsUpdatePrompt(window) &&
                                (window.Visible || (window.Class.StartsWith("HwndWrapper[", StringComparison.Ordinal) &&
                                 window.Title.Length != 0 && window.Title != "MediaContextNotificationWindow" &&
                                 window.Title != "SystemResourceNotifyWindow" && window.Title != "Hidden Window"))) return "Busy";
                        // An old version's update notification is not an unsaved screenshot.
                        // Close its modal frame normally before asking the outer WPF loop to exit.
                        foreach (var window in windows)
                            if (IsUpdatePrompt(window) && SendMessageTimeout(window.Handle, 0x0010,
                                IntPtr.Zero, IntPtr.Zero, 2, 2000, out response) == IntPtr.Zero) return "Unresponsive";
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
        private static bool IsUpdatePrompt(WindowInfo window)
        {
            return window.Title == UpdatePromptTitle && window.Class.StartsWith("HwndWrapper[", StringComparison.Ordinal);
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
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder name);
    }
}
