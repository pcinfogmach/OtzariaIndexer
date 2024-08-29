using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OtzriaIndexerTextFilesOnly
{
    public static class MemoryManager
    {
        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);
        public static void CleanAsync()
        {
            Task.Factory.StartNew(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            });          
        }

        public static void Clean()
        {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            
        }

        public static bool MemoryExceedsLimit()
        {
            const long oneGB = 1L * 1024 * 1024 * 1024;

            // Get the current memory usage of the application
            Process currentProcess = Process.GetCurrentProcess();
            long memoryUsed = currentProcess.WorkingSet64;

            return memoryUsed > oneGB;
        }
    }
}
