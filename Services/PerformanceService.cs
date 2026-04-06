using System.Diagnostics;

namespace NBH.Services
{
    /// <summary>
    /// Einfacher Performance-Helper für Debug-Messungen.
    /// Nur im DEBUG-Build aktiv – im Release wird alles wegoptimiert.
    /// </summary>
    public static class PerformanceService
    {
        /// <summary>
        /// Misst die Dauer eines Code-Blocks und gibt sie im Debug-Output aus.
        /// Verwendung: using (PerformanceService.MeasureOperation("LoadEntries")) { ... }
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogStart(string operationName)
        {
            Debug.WriteLine($"[PERF] {operationName}: START");
        }

        /// <summary>
        /// Erstellt einen IDisposable-Wrapper, der die Dauer beim Dispose ausgibt.
        /// </summary>
        public static IDisposable MeasureOperation(string operationName)
        {
            return new PerformanceMeasurement(operationName);
        }

        private sealed class PerformanceMeasurement : IDisposable
        {
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;

            public PerformanceMeasurement(string operationName)
            {
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                Debug.WriteLine($"[PERF] {_operationName}: {_stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
