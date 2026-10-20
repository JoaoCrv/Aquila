using System.IO;
using System.Runtime.InteropServices;

namespace Aquila.Services.Processes;

/// <summary>
/// Every process on the machine, in one call to the kernel — the call Task Manager makes.
///
/// Not the .NET <c>Process</c> API, and measured rather than assumed (2026-10-19, ~373 processes): that one opens
/// every process to ask about it, took 5.4–6.6 ms, and unelevated could not open 140 of them (38%) — the services
/// and anything else not the user's own. This one opens nothing, so it reads every process elevated or not, in
/// 4.1–4.5 ms, and it carries the I/O counts the .NET API does not have at all.
/// </summary>
public sealed class ProcessTable : IDisposable
{
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int needed);

    private const int SystemProcessInformation = 5;
    private const int InfoLengthMismatch = unchecked((int)0xC0000004);

    // Grown when the table outgrows it and then kept, so a read allocates nothing but the samples.
    private IntPtr _buffer;
    private int _size;

    public List<ProcessSample> Read()
    {
        while (true)
        {
            if (_buffer == IntPtr.Zero)
            {
                _size = Math.Max(_size, 512 * 1024);
                _buffer = Marshal.AllocHGlobal(_size);
            }

            int status = NtQuerySystemInformation(SystemProcessInformation, _buffer, _size, out int needed);
            if (status == 0) break;
            if (status != InfoLengthMismatch)
                throw new InvalidOperationException($"NtQuerySystemInformation failed: 0x{status:X8}");

            // Room to spare, because processes start between asking how much is needed and asking again.
            Free();
            _size = Math.Max(needed, _size) + 64 * 1024;
        }

        var samples = new List<ProcessSample>(512);
        for (int offset = 0; ; )
        {
            var entry = Marshal.PtrToStructure<Entry>(_buffer + offset);

            // The idle process has no name, and it is not an application: listing it would put "Idle 95%" at the
            // top of every quiet machine's CPU list.
            if (entry.ImageName.Buffer != IntPtr.Zero)
            {
                var image = Marshal.PtrToStringUni(entry.ImageName.Buffer, entry.ImageName.Length / 2);
                samples.Add(new ProcessSample(
                    (long)entry.UniqueProcessId,
                    entry.CreateTime,
                    Path.GetFileNameWithoutExtension(image),
                    entry.UserTime + entry.KernelTime,
                    entry.WorkingSetPrivateSize,
                    entry.ReadTransferCount + entry.WriteTransferCount));
            }

            if (entry.NextEntryOffset == 0) break;
            offset += (int)entry.NextEntryOffset;
        }

        return samples;
    }

    public void Dispose() => Free();

    private void Free()
    {
        if (_buffer == IntPtr.Zero) return;
        Marshal.FreeHGlobal(_buffer);
        _buffer = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    /// <summary>
    /// <c>SYSTEM_PROCESS_INFORMATION</c>, whole. <c>winternl.h</c> documents only some of its fields and hides the
    /// rest as reserved; this is the full layout as phnt (Process Hacker's headers) declares it, which is what the
    /// offsets of the fields read here depend on. The pointer-sized fields keep it right for either bitness.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Entry
    {
        public uint NextEntryOffset;
        public uint NumberOfThreads;
        /// <summary>The private working set — what Task Manager's Memory column shows.</summary>
        public long WorkingSetPrivateSize;
        public uint HardFaultCount;
        public uint NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long CreateTime;
        public long UserTime;
        public long KernelTime;
        public UnicodeString ImageName;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
        public uint HandleCount;
        public uint SessionId;
        public UIntPtr UniqueProcessKey;
        public UIntPtr PeakVirtualSize;
        public UIntPtr VirtualSize;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivatePageCount;
        public long ReadOperationCount;
        public long WriteOperationCount;
        public long OtherOperationCount;
        public long ReadTransferCount;
        public long WriteTransferCount;
        public long OtherTransferCount;
    }
}
