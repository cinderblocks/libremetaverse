using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WebRtcTest
{
    // Small helper to write a native minidump when unhandled exceptions occur.
    // This aids diagnosing native crashes (heap corruption) by producing a .dmp file
    // that can be opened in Visual Studio for mixed-mode/native debugging.
    public static class NativeCrashDumper
    {
        [Flags]
        private enum MINIDUMP_TYPE : uint
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpFilterMemory = 0x00000008,
            MiniDumpScanMemory = 0x00000010,
            MiniDumpWithUnloadedModules = 0x00000020,
            MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
            MiniDumpFilterModulePaths = 0x00000080,
            MiniDumpWithProcessThreadData = 0x00000100,
            MiniDumpWithPrivateReadWriteMemory = 0x00000200,
            MiniDumpWithoutOptionalData = 0x00000400,
            MiniDumpWithFullMemoryInfo = 0x00000800,
            MiniDumpWithThreadInfo = 0x00001000,
            MiniDumpWithCodeSegs = 0x00002000,
            MiniDumpWithoutAuxiliaryState = 0x00004000,
            MiniDumpWithFullAuxiliaryState = 0x00008000,
            MiniDumpWithPrivateWriteCopyMemory = 0x00010000,
            MiniDumpIgnoreInaccessibleMemory = 0x00020000,
            MiniDumpWithTokenInformation = 0x00040000
        }

        [DllImport("Dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            IntPtr hFile,
            MINIDUMP_TYPE dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        public static void InstallCrashHandler()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    try { WriteMiniDump("AppDomain_Unhandled", e.ExceptionObject as Exception); } catch { }
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try { WriteMiniDump("TaskScheduler_Unobserved", e.Exception); } catch { }
                };

                // Also trap process exit to attempt a final dump if required
                Process.GetCurrentProcess().EnableRaisingEvents = true;
                Process.GetCurrentProcess().Exited += (s, e) =>
                {
                    try { WriteMiniDump("Process_Exited", null); } catch { }
                };

                Console.WriteLine("[NativeCrashDumper] Installed crash handlers. For native debugging enable 'Mixed Mode' in Visual Studio (Debug->Options->Debugging->" +
                    "General -> 'Enable native code debugging') and open the generated .dmp files with VS.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NativeCrashDumper] Failed to install handlers: " + ex.Message);
            }
        }

        private static void WriteMiniDump(string tag, Exception ex)
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                Directory.CreateDirectory(baseDir);
                var file = Path.Combine(baseDir, $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{tag}.dmp");

                using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var success = MiniDumpWriteDump(proc.Handle, (uint)proc.Id, fs.SafeFileHandle.DangerousGetHandle(),
                        MINIDUMP_TYPE.MiniDumpWithFullMemory | MINIDUMP_TYPE.MiniDumpWithHandleData | MINIDUMP_TYPE.MiniDumpWithUnloadedModules,
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                    if (success)
                    {
                        Console.WriteLine($"[NativeCrashDumper] Wrote mini dump to {file}");
                    }
                    else
                    {
                        Console.WriteLine($"[NativeCrashDumper] MiniDumpWriteDump failed, error={Marshal.GetLastWin32Error()}");
                    }
                }

                // Also write a text summary for convenience
                try
                {
                    var txt = Path.ChangeExtension(file, ".txt");
                    using (var tw = new StreamWriter(txt, false))
                    {
                        tw.WriteLine($"Dump: {file}");
                        tw.WriteLine($"Time: {DateTime.UtcNow:o}");
                        tw.WriteLine($"Process: {proc.ProcessName} ({proc.Id})");
                        tw.WriteLine($"Tag: {tag}");
                        if (ex != null)
                        {
                            tw.WriteLine("Exception: " + ex.GetType().FullName);
                            tw.WriteLine(ex.ToString());
                        }
                    }
                }
                catch { }
            }
            catch (Exception dumpEx)
            {
                Console.WriteLine("[NativeCrashDumper] Failed to write dump: " + dumpEx.Message);
            }
        }
    }
}
