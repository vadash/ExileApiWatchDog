using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
// ReSharper disable InconsistentNaming

namespace ExileApiWatchDog
{
    internal static class Program
    {
        private const string GAME_PROC_NAME = "PathOfExile_x64";
        private const string EXILE_API_PROC_NAME = "Loader";
        private const string NO_OWNER = "NO_OWNER";
        private static Process _hud;
        private static Process _game;
        private static string _gameOwner = "";
        private static string _hudOwner = "";
        private static readonly Stopwatch _hudUnresponsive = new Stopwatch();
        private static readonly Stopwatch _gameUnresponsive = new Stopwatch();

        #region WINAPI

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);
        
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

        private delegate bool HandlerRoutine(ControlTypes CtrlType);
        
        public enum ControlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        #endregion
       
        public static void Main()
        {
            Console.WriteLine($"ExileApiWatchDog v{Assembly.GetExecutingAssembly().GetName().Version}");
            using (new Mutex(
                true,
                "ExileApiWatchDog",
                out var createdNew))
            {
                if (!createdNew) return;

                // Set event OnFormClose
                SetConsoleCtrlHandler(OnFormClose, true);
                
                // Minimize process
                ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 6);
                
                for (var i = 0; ; i++)
                {
                    try
                    {
                        CheckExileApi(i);
                        CheckGame(i);
                        CheckLimitedUser(i);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            // ReSharper disable once FunctionNeverReturns
        }

        private static void CheckLimitedUser(int counter)
        {
            if (_game != null &&
                _hud != null &&
                _hudOwner == _gameOwner)
            {
                Console.WriteLine(
                    $"{counter:X7} ExileApi and PoE are running under same user. Please configure it correctly");
                CloseExileApi();
            }
        }

        private static void CheckExileApi(int counter)
        {
            if (_hud?.Responding == true)
                _hudUnresponsive?.Reset();
            else if (_hud?.Responding == false) 
                _hudUnresponsive?.Start();

            if (_hud != null &&
                _hudUnresponsive?.ElapsedMilliseconds > 60000)
            {
                Console.WriteLine(
                    $"{counter:X7} ExileApi is frozen. Restarting");
                CloseExileApi();
            }
            
            if (_game == null)
            {
                Console.WriteLine($"{counter:X7} Game is not running. Idling...");
                Thread.Sleep(5000);
                return;
            }
            
            // Update _hudOwner
            if (_hud == null ||
                _hud.HasExited ||
                _hudOwner == NO_OWNER)
            {
                _hud = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == EXILE_API_PROC_NAME);
                _hudOwner = GetProcessOwner(_hud?.Id);
            }

            // Auto start HUD
            if (_hud == null && // hud is not started
                _gameUnresponsive?.ElapsedMilliseconds == 0) // and game is responsive
            {
                var startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    FileName = EXILE_API_PROC_NAME + ".exe"
                };
                Console.WriteLine(
                    $"{counter:X7} Starting ExileApi process {startInfo.FileName} from {startInfo.WorkingDirectory} directory");
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
            
            Thread.Sleep(500);
        }

        private static void CheckGame(int counter)
        {
            if (_game?.Responding == true)
                _gameUnresponsive?.Reset();
            else if (_game?.Responding == false) 
                _gameUnresponsive?.Start();

            if (_game != null &&
                _gameUnresponsive?.ElapsedMilliseconds > 0)
            {
                Console.WriteLine(
                    $"{counter:X7} PoE is frozen {_gameUnresponsive?.ElapsedMilliseconds} ms");
            }
            
            if (_game != null &&
                _gameUnresponsive?.ElapsedMilliseconds > 60000)
            {
                Console.WriteLine(
                    $"{counter:X7} PoE is frozen. Restarting");
                ClosePoe();
                Thread.Sleep(5000);
            }
            
            if (_game == null)
            {
                Console.WriteLine(
                    $"{counter:X7} Starting PoE");
                StartPoe(counter);
            }
            
            // Update _gameOwner
            if (_game == null ||
                _game.HasExited ||
                _gameOwner == NO_OWNER)
            {
                _game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == GAME_PROC_NAME);
                _gameOwner = GetProcessOwner(_game?.Id);
            }
        }

        private static bool OnFormClose(ControlTypes ctrlType)
        {
            return CloseExileApi();
        }

        private static bool CloseExileApi()
        {
            try
            {
                _hudUnresponsive?.Reset();
                Console.Beep();
                _hud?.Kill();
                _hud = null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return true;
        }
        
        private static void ClosePoe()
        {
            try
            {
                _gameUnresponsive?.Reset();
                Console.Beep();
                _game?.Kill();
                _game = null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        
        private static void StartPoe(int counter)
        {
            Console.WriteLine(
                $"{counter:X7} Starting Path of Exile under limited user");
            try
            {
                Process.Start("StartPathOfExile.cmd");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(15000);
            }
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