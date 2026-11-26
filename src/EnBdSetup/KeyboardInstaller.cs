using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EnBdSetup
{
    internal static class KeyboardInstaller
    {
        private const string LayoutsKeyPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";
        private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr hProcess, out ushort pProcessMachine, out ushort pNativeMachine);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        private static RegistryKey OpenNativeLocalMachine()
        {
            return RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
        }

        private static string DetectNativeMachineArch()
        {
            try
            {
                ushort procMachine;
                ushort nativeMachine;
                if (IsWow64Process2(GetCurrentProcess(), out procMachine, out nativeMachine))
                {
                    if (nativeMachine == 0xAA64) return "arm64";
                    if (nativeMachine == 0x8664) return "x64";
                    if (nativeMachine == 0x014C) return "x86";
                }
            }
            catch
            {
                // Older builds such as Windows Server 2016 / Win10 1607-1703 lack IsWow64Process2 (x86/x64 only).
            }

            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }

        private static void ExtractResourceToFile(string resourceName, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    throw new InvalidOperationException("Missing embedded keyboard resource: " + resourceName);

                string tempPath = targetPath + ".new";
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                }

                if (File.Exists(targetPath))
                {
                    try
                    {
                        File.Delete(targetPath);
                    }
                    catch
                    {
                        string oldPath = targetPath + ".old." + Guid.NewGuid().ToString("N").Substring(0, 6);
                        File.Move(targetPath, oldPath);
                        MoveFileEx(oldPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                    }
                }

                File.Move(tempPath, targetPath);
            }
        }

        private static string FindOrAllocateKlid(RegistryKey layoutsRoot)
        {
            foreach (string subName in layoutsRoot.GetSubKeyNames())
            {
                using (RegistryKey sub = layoutsRoot.OpenSubKey(subName))
                {
                    if (sub == null) continue;
                    string file = sub.GetValue("Layout File") as string;
                    if (string.Equals(file, Constants.KeyboardLayoutFile, StringComparison.OrdinalIgnoreCase))
                    {
                        return subName.ToLowerInvariant();
                    }
                }
            }

            // Allocate next free custom KLID in a0000409 .. a0ff0409
            for (int idx = 0; idx <= 0xFF; idx++)
            {
                string candidate = string.Format(CultureInfo.InvariantCulture, "a0{0:x2}0409", idx);
                using (RegistryKey existing = layoutsRoot.OpenSubKey(candidate))
                {
                    if (existing == null)
                        return candidate;
                }
            }

            throw new InvalidOperationException("No free custom keyboard layout ID slot available.");
        }

        private static string FindOrAllocateLayoutId(RegistryKey layoutsRoot, string ownKlid)
        {
            var usedIds = new HashSet<int>();
            int existingOwnId = -1;

            foreach (string subName in layoutsRoot.GetSubKeyNames())
            {
                using (RegistryKey sub = layoutsRoot.OpenSubKey(subName))
                {
                    if (sub == null) continue;
                    string idStr = sub.GetValue("Layout Id") as string;
                    if (string.IsNullOrWhiteSpace(idStr)) continue;

                    int parsed;
                    if (!int.TryParse(idStr.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                        continue;

                    if (string.Equals(subName, ownKlid, StringComparison.OrdinalIgnoreCase))
                    {
                        existingOwnId = parsed;
                    }
                    else
                    {
                        usedIds.Add(parsed);
                    }
                }
            }

            // Reuse existing valid MSKLC-range custom Layout Id (00c0..00ff) if not conflicting
            if (existingOwnId >= 0x00C0 && existingOwnId <= 0x00FF && !usedIds.Contains(existingOwnId))
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:x4}", existingOwnId);
            }

            // Allocate first unused custom Layout Id in 00c0..00ff (MSKLC standard)
            for (int id = 0x00C0; id <= 0x00FF; id++)
            {
                if (!usedIds.Contains(id))
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0:x4}", id);
                }
            }

            throw new InvalidOperationException("No free custom Layout Id (00c0-00ff) available.");
        }

        public static string InstallKeyboardLayout(Action<string> log)
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string arch = DetectNativeMachineArch();
            string nativeSystem32 = (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                ? Path.Combine(winDir, "Sysnative")
                : Path.Combine(winDir, "System32");

            log("Installing keyboard layout DLL (" + arch + ")...");

            string nativeDllPath = Path.Combine(nativeSystem32, Constants.KeyboardLayoutFile);
            ExtractResourceToFile("EnBdSetup.kbd." + arch + ".kbdusbd.dll", nativeDllPath);
            log("Installed " + nativeDllPath);

            if (Environment.Is64BitOperatingSystem)
            {
                string wow64Dir = Path.Combine(winDir, "SysWOW64");
                if (Directory.Exists(wow64Dir))
                {
                    string wowDllPath = Path.Combine(wow64Dir, Constants.KeyboardLayoutFile);
                    ExtractResourceToFile("EnBdSetup.kbd.wow64.kbdusbd.dll", wowDllPath);
                    log("Installed WOW64 compatibility DLL in SysWOW64.");
                }
            }

            using (RegistryKey hklm = OpenNativeLocalMachine())
            using (RegistryKey layoutsRoot = hklm.OpenSubKey(LayoutsKeyPath, true))
            {
                if (layoutsRoot == null)
                    throw new InvalidOperationException("Cannot open Keyboard Layouts registry key.");

                string klid = FindOrAllocateKlid(layoutsRoot);
                string layoutId = FindOrAllocateLayoutId(layoutsRoot, klid);

                using (RegistryKey kbdKey = layoutsRoot.CreateSubKey(klid))
                {
                    kbdKey.SetValue("Layout Text", Constants.KeyboardLayoutName, RegistryValueKind.String);
                    kbdKey.SetValue("Layout Display Name", Constants.KeyboardLayoutDisplayName, RegistryValueKind.String);
                    kbdKey.SetValue("Layout File", Constants.KeyboardLayoutFile, RegistryValueKind.String);
                    kbdKey.SetValue("Layout Id", layoutId, RegistryValueKind.String);
                }

                log("Registered keyboard layout '" + Constants.KeyboardLayoutName + "' (KLID " + klid + ", Layout Id " + layoutId + ").");
                return klid;
            }
        }

        public static void UninstallKeyboardLayout(Action<string> log)
        {
            try
            {
                using (RegistryKey hklm = OpenNativeLocalMachine())
                using (RegistryKey layoutsRoot = hklm.OpenSubKey(LayoutsKeyPath, true))
                {
                    if (layoutsRoot != null)
                    {
                        foreach (string subName in layoutsRoot.GetSubKeyNames())
                        {
                            bool match = false;
                            using (RegistryKey sub = layoutsRoot.OpenSubKey(subName))
                            {
                                if (sub != null)
                                {
                                    string file = sub.GetValue("Layout File") as string;
                                    if (string.Equals(file, Constants.KeyboardLayoutFile, StringComparison.OrdinalIgnoreCase))
                                        match = true;
                                }
                            }
                            if (match)
                            {
                                layoutsRoot.DeleteSubKeyTree(subName, false);
                                log("Removed keyboard layout registry entry (" + subName + ").");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log("Note removing keyboard registry key: " + ex.Message);
            }

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string nativeSystem32 = (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                ? Path.Combine(winDir, "Sysnative")
                : Path.Combine(winDir, "System32");

            string[] paths = new[]
            {
                Path.Combine(nativeSystem32, Constants.KeyboardLayoutFile),
                Path.Combine(winDir, "SysWOW64", Constants.KeyboardLayoutFile)
            };

            foreach (string dllPath in paths)
            {
                try
                {
                    if (File.Exists(dllPath))
                    {
                        try
                        {
                            File.Delete(dllPath);
                            log("Removed " + dllPath);
                        }
                        catch
                        {
                            MoveFileEx(dllPath, null, MOVEFILE_DELAY_UNTIL_REBOOT);
                            log("Scheduled removal of " + dllPath + " on reboot.");
                        }
                    }
                }
                catch { }
            }
        }
    }
}
