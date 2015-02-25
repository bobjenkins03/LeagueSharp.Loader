﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using LeagueSharp.Loader.Model.Assembly;
using LeagueSharp.Loader.Model.Log;
using LeagueSharp.Loader.Model.Settings;

namespace LeagueSharp.Loader.Core
{
    internal class Injector
    {
        public delegate void OnInjectDelegate(IntPtr hwnd);

        private static InjectDLLDelegate injectDLL;

        public static bool IsInjected
        {
            get { return LeagueProcess.Any(IsProcessInjected); }
        }

        public static List<IntPtr> LeagueInstances
        {
            get { return FindWindows("League of Legends (TM) Client"); }
        }

        private static List<Process> LeagueProcess
        {
            get { return Process.GetProcessesByName("League of Legends").ToList(); }
        }

        public static event OnInjectDelegate OnInject;

        private static bool IsProcessInjected(Process leagueProcess)
        {
            if (leagueProcess != null)
            {
                try
                {
                    return
                        leagueProcess.Modules.Cast<ProcessModule>()
                            .Any(
                                processModule => processModule.ModuleName == Path.GetFileName(Directories.CoreFilePath));
                }
                catch (Exception e)
                {
                    Utility.Log(LogLevel.Error, string.Format("Error - {0}", e));
                }
            }
            return false;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(IntPtr ZeroOnly, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private static string GetWindowText(IntPtr hWnd)
        {
            var size = GetWindowTextLength(hWnd);
            if (size++ > 0)
            {
                var builder = new StringBuilder(size);
                GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }
            return String.Empty;
        }

        private static List<IntPtr> FindWindows(string title)
        {
            var windows = new List<IntPtr>();
            EnumWindows(delegate(IntPtr wnd, IntPtr param)
            {
                if (GetWindowText(wnd).Contains(title))
                {
                    windows.Add(wnd);
                }
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        private static void ResolveInjectDLL()
        {
            var hModule = LoadLibrary(Directories.BootstrapFilePath);
            if (!(hModule != IntPtr.Zero))
            {
                return;
            }
            var procAddress = GetProcAddress(hModule, "_InjectDLL@8");
            if (!(procAddress != IntPtr.Zero))
            {
                return;
            }
            injectDLL =
                Marshal.GetDelegateForFunctionPointer(procAddress, typeof(InjectDLLDelegate)) as InjectDLLDelegate;
        }

        public static void Pulse()
        {
            if (LeagueProcess == null)
            {
                return;
            }

            //Don't inject untill we checked that there are not updates for the loader.
            if (AppUpdater.Updating || !AppUpdater.CheckedForUpdates)
            {
                return;
            }

            foreach (var instance in LeagueProcess)
            {
                try
                {
                    Config.Instance.LeagueOfLegendsExePath = instance.Modules[0].FileName;
                    if (!IsProcessInjected(instance) && AppUpdater.UpdateCore(instance.Modules[0].FileName, true).Item1)
                    {
                        if (injectDLL == null)
                        {
                            ResolveInjectDLL();
                        }
                        if (injectDLL != null &&
                            GetWindowText(instance.MainWindowHandle).Contains("League of Legends (TM) Client"))
                        {
                            injectDLL(instance.Id, Directories.CoreFilePath);
                            if (OnInject != null)
                            {
                                OnInject(instance.MainWindowHandle);
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static void LoadAssembly(IntPtr wnd, LeagueSharpAssembly assembly)
        {
            if (assembly.Type != AssemblyType.Unknown && assembly.Type != AssemblyType.Library && assembly.State == AssemblyState.Ready)
            {
                var str = string.Format("load \"{0}\"", assembly.PathToBinary);
                var lParam = new COPYDATASTRUCT { cbData = 1, dwData = str.Length * 2 + 2, lpData = str };
                SendMessage(wnd, 74U, IntPtr.Zero, ref lParam);
            }
        }

        public static void UnloadAssembly(IntPtr wnd, LeagueSharpAssembly assembly)
        {
            if (assembly.Type != AssemblyType.Unknown && assembly.Type != AssemblyType.Library && assembly.State == AssemblyState.Ready)
            {
                var str = string.Format("unload \"{0}\"", Path.GetFileName(assembly.PathToBinary));
                var lParam = new COPYDATASTRUCT { cbData = 1, dwData = str.Length * 2 + 2, lpData = str };
                SendMessage(wnd, 74U, IntPtr.Zero, ref lParam);
            }
        }

        public static void SendLoginCredentials(IntPtr wnd, string user, string passwordHash)
        {
            var str = string.Format("LOGIN|{0}|{1}", user, passwordHash);
            var lParam = new COPYDATASTRUCT { cbData = 2, dwData = str.Length * 2 + 2, lpData = str };
            SendMessage(wnd, 74U, IntPtr.Zero, ref lParam);
        }

        public static void SendConfig(IntPtr wnd)
        {
            var str = string.Format(
                "{0}{1}{2}{3}", (Config.Instance.Settings.GameSettings[0].SelectedValue == "True") ? "1" : "0",
                (Config.Instance.Settings.GameSettings[3].SelectedValue == "True") ? "1" : "0",
                (Config.Instance.Settings.GameSettings[1].SelectedValue == "True") ? "1" : "0",
                (Config.Instance.Settings.GameSettings[2].SelectedValue == "True") ? "2" : "0");
            var lParam = new COPYDATASTRUCT { cbData = 2, dwData = str.Length * 2 + 2, lpData = str };
            SendMessage(wnd, 74U, IntPtr.Zero, ref lParam);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool InjectDLLDelegate(int processId, string path);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public struct COPYDATASTRUCT
        {
            public int cbData;
            public int dwData;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpData;
        }
    }
}