using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable PossibleMultipleEnumeration

namespace ExileApiWatchDog
{
    internal static class Program
    {
        private const int POE_TIMEOUT_MS = 45000;
        private const int HUD_TIMEOUT_MS = 45000;

        private const string GAME_PROC_NAME = "PathOfExile_x64";
        private const string EXILE_API_PROC_NAME = "Loader";
        private const string NO_OWNER = "NO_OWNER";
        private static Process _hud;
        private static Process _game;
        private static string _gameOwner = NO_OWNER;
        private static string _hudOwner = NO_OWNER;
        private static readonly Stopwatch _hudUnresponsive = new Stopwatch();
        private static readonly Stopwatch _gameUnresponsive = new Stopwatch();

        #region WINAPI

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        
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
                        CheckGame(i);
                        CheckExileApi(i);
                        CheckLimitedUser(i);
                        Thread.Sleep(1000);
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
                _gameOwner != NO_OWNER &&
                _hudOwner != NO_OWNER &&
                _hudOwner == _gameOwner)
            {
                Console.WriteLine(
                    $"{counter:X7} ExileApi and PoE are running under same user. Please configure it correctly");
                CloseExileApi();
                Console.WriteLine($"{counter:X7} Sleeping for 10 seconds");
                Thread.Sleep(10000);
            }
        }

        private static void CheckExileApi(int counter)
        {
            // Updating _hud
            _hud = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == EXILE_API_PROC_NAME);
            if (_hud == null)
            {
                StartExileApi(counter);
            }
            else
            {
                // Updating _hudOwner and _hudUnresponsive
                _hudOwner = GetProcessOwner(_hud?.Id);
                if (_hud?.Responding == true)
                    _hudUnresponsive?.Reset();
                else if (_hud?.Responding == false) 
                    _hudUnresponsive?.Start();

                // Frozen check
                if (_hud != null &&
                    _hudUnresponsive?.ElapsedMilliseconds > HUD_TIMEOUT_MS / 5)
                {
                    Console.WriteLine(
                        $"{counter:X7} HUD is frozen for {_hudUnresponsive?.ElapsedMilliseconds} ms");
                }
                if (_hud != null &&
                    _hudUnresponsive?.ElapsedMilliseconds > HUD_TIMEOUT_MS)
                {
                    Console.WriteLine(
                        $"{counter:X7} ExileApi is frozen for over {HUD_TIMEOUT_MS} MS. Killing it");
                    CloseExileApi();
                    Console.WriteLine($"{counter:X7} Sleeping for 10 seconds");
                    Thread.Sleep(10000);
                }
            }
        }

        private static void CheckGame(int counter)
        {
            // Updating _game
            _game = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == GAME_PROC_NAME);
            if (_game == null)
            {
                StartPoe(counter);
            }
            else
            {
                // Updating _gameOwner and _gameUnresponsive timer
                _gameOwner = GetProcessOwner(_game?.Id);
                if (_game?.Responding == true)
                    _gameUnresponsive?.Reset();
                else if (_game?.Responding == false)
                    _gameUnresponsive?.Start();

                // Frozen check
                if (_game != null &&
                    _gameUnresponsive?.ElapsedMilliseconds > POE_TIMEOUT_MS / 5)
                {
                    Console.WriteLine(
                        $"{counter:X7} PoE is frozen {_gameUnresponsive?.ElapsedMilliseconds} ms");
                }
                if (_game != null &&
                    _gameUnresponsive?.ElapsedMilliseconds > POE_TIMEOUT_MS)
                {
                    Console.WriteLine(
                        $"{counter:X7} PoE is frozen for over {POE_TIMEOUT_MS} ms. Killing ExileApi and PoE");
                    ClosePoe();
                    CloseExileApi();
                    Console.WriteLine($"{counter:X7} Sleeping for 10 seconds");
                    Thread.Sleep(10000);
                }

                CloseAppError();
            }
        }

        private static void CloseAppError()
        {
            var allProc = Process.GetProcesses();
            var procs1 = allProc
                .Where(pr =>
                    pr.MainWindowTitle.ToLower().Contains("pathofexile") ||
                    pr.MainWindowTitle.ToLower().Contains("exileapi"))
                .Where(pr =>
                    pr.MainWindowTitle.ToLower().Contains("ошибка") ||
                    pr.MainWindowTitle.ToLower().Contains("error"));
            // Not enough memory resources are available to complete this operation
            var procs2 = allProc
                .Where(pr =>
                    pr.MainWindowTitle.ToLower().Contains("exception"));
            var procs = procs1.Concat(procs2);
            if (procs.Any())
            {
                SetForegroundWindow(procs.First().MainWindowHandle);
                Thread.Sleep(1000);
                SendKeys.SendWait("{Enter}");
            }
        }

        private static bool OnFormClose(ControlTypes ctrlType)
        {
            return CloseExileApi();
        }

        private static bool StartExileApi(int counter)
        {
            try
            {
                // Start HUD
                if (_game != null &&
                    _game?.HasExited == false &&
                    _game?.Responding == true &&
                    _hud == null)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = Directory.GetCurrentDirectory(),
                        FileName = EXILE_API_PROC_NAME + ".exe"
                    };
                    Console.WriteLine(
                        $"{counter:X7} Starting ExileApi process {startInfo.FileName} from {startInfo.WorkingDirectory} directory");
                    _hud = Process.Start(startInfo);
                    Console.WriteLine($"{counter:X7} Sleeping for 10 seconds");
                    Thread.Sleep(10000);
                    _hudOwner = NO_OWNER;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(10000);
            }
            return true;
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
                Thread.Sleep(5000);
            }
            return true;
        }
        
        private static void StartPoe(int counter)
        {
            try
            {
                if (_game == null)
                {
                    Console.WriteLine(
                        $"{counter:X7} Starting Path of Exile under limited user");
                    CloseExileApi();
                    _game = Process.Start("StartPathOfExile.cmd");
                    Console.WriteLine($"{counter:X7} Sleeping for 10 seconds");
                    Thread.Sleep(10000);
                    _gameOwner = NO_OWNER;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(10000);
            }
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
                Thread.Sleep(5000);
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