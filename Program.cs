using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExileApiWatchDog
{
    internal static class Program
    {
        private const string GAME_PROC_NAME = "PathOfExile_x64";
        private const string EXILE_API_PROC_NAME = "Loader";
        private const string NO_OWNER = "NO_OWNER";

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);
        
        public static void Main()
        {
            using (new Mutex(
                true,
                "ExileApiWatchDog",
                out var createdNew))
            {
                if (!createdNew) return;

                // Hide process
                ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 6);
                
                Process game = null;
                var gameOwner = "";
                Process hud = null;
                var hudOwner = "";

                while (true)
                {
                    if (game != null &&
                        hud != null &&
                        hudOwner == gameOwner)
                    {
                        Console.WriteLine(
                            "ExileApi and PoE are running under same user. Please configure it correctly");
                        Console.Beep();
                    }

                    if (game == null ||
                        game.HasExited ||
                        gameOwner == NO_OWNER)
                    {
                        game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == GAME_PROC_NAME);
                        gameOwner = GetProcessOwner(game?.Id);
                    }

                    if (game == null)
                    {
                        Console.WriteLine("Game is not running. Idling...");
                        Thread.Sleep(5000);
                        continue;
                    }

                    if (hud == null ||
                        hud.HasExited ||
                        hudOwner == NO_OWNER)
                    {
                        hud = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == EXILE_API_PROC_NAME);
                        hudOwner = GetProcessOwner(hud?.Id);
                    }

                    if (hud == null)
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = Directory.GetCurrentDirectory(),
                            FileName = EXILE_API_PROC_NAME + ".exe"
                        };
                        Console.WriteLine(
                            $"Starting ExileApi process {startInfo.FileName} from {startInfo.WorkingDirectory} directory");
                        try
                        {
                            Process.Start(startInfo);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            Thread.Sleep(15000);
                        }

                        Thread.Sleep(5000);
                    }

                    Console.WriteLine($"All good hud owner [{hudOwner}] != game owner [{gameOwner}]. Idling...");
                    Thread.Sleep(500);
                }
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static string GetProcessOwner(int? processId)
        {
            if (processId == null) return NO_OWNER;
            var query = @"Select * From Win32_Process Where ProcessID = " + processId;
            var searcher = new ManagementObjectSearcher(query);
            var processList = searcher.Get();

            foreach (var o in processList)
            {
                var obj = (ManagementObject) o;
                object[] argList = { string.Empty, string.Empty };
                var returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
                if (returnVal == 0)
                {
                    // return DOMAIN\user
                    return argList[1] + "\\" + argList[0];
                }
            }
            
            return NO_OWNER;
        }
    }
}