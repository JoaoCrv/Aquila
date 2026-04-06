using System;
using System.Runtime.InteropServices;

namespace Aquila.Services
{
    /// <summary>
    /// Reads Windows-specific memory and paging metrics that are not provided directly by LibreHardwareMonitor.
    /// </summary>
    public sealed class WindowsMetricsReader : IDisposable
    {
        private IntPtr _pdhQuery;
        private IntPtr _pdhPageReads;
        private IntPtr _pdhPageWrites;
        private bool _initialized;
        private bool _disposed;

        public void Open()
        {
            ThrowIfDisposed();

            if (_initialized)
                return;

            _initialized = true;

            if (PdhOpenQuery(IntPtr.Zero, IntPtr.Zero, out _pdhQuery) == 0)
            {
                PdhAddEnglishCounter(_pdhQuery, @"\Memory\Page Reads/sec", IntPtr.Zero, out _pdhPageReads);
                PdhAddEnglishCounter(_pdhQuery, @"\Memory\Page Writes/sec", IntPtr.Zero, out _pdhPageWrites);
                PdhCollectQueryData(_pdhQuery); // baseline collection for rate counters
            }
        }

        public WindowsMetricsSnapshot ReadSnapshot()
        {
            ThrowIfDisposed();
            Open();

            long cacheBytes = 0;
            float pageReadsPerSec = 0;
            float pageWritesPerSec = 0;

            try
            {
                if (GetPerformanceInfo(out var performanceInfo, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
                    cacheBytes = (long)performanceInfo.SystemCache * (long)performanceInfo.PageSize;

                if (_pdhQuery != IntPtr.Zero && PdhCollectQueryData(_pdhQuery) == 0)
                {
                    if (PdhGetFormattedCounterValue(_pdhPageReads, PDH_FMT_DOUBLE, out _, out var reads) == 0)
                        pageReadsPerSec = (float)reads.doubleValue;

                    if (PdhGetFormattedCounterValue(_pdhPageWrites, PDH_FMT_DOUBLE, out _, out var writes) == 0)
                        pageWritesPerSec = (float)writes.doubleValue;
                }
            }
            catch
            {
                // Keep metric reading resilient; failures here should not stop hardware polling.
            }

            return new WindowsMetricsSnapshot(pageReadsPerSec, pageWritesPerSec, cacheBytes);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_pdhQuery != IntPtr.Zero)
            {
                PdhCloseQuery(_pdhQuery);
                _pdhQuery = IntPtr.Zero;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb, CommitTotal, CommitLimit, CommitPeak,
                         PhysicalTotal, PhysicalAvailable, SystemCache,
                         KernelTotal, KernelPaged, KernelNonpaged, PageSize,
                         HandleCount, ProcessCount, ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE
        {
            [FieldOffset(0)] public uint CStatus;
            [FieldOffset(8)] public double doubleValue;
        }

        private const uint PDH_FMT_DOUBLE = 0x00000200;

        [DllImport("pdh.dll")]
        private static extern int PdhOpenQuery(IntPtr dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint counterType, out PDH_FMT_COUNTERVALUE value);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr query);
    }

    public readonly record struct WindowsMetricsSnapshot(float PageReadsPerSec, float PageWritesPerSec, long CacheBytes);
}
