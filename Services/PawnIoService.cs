using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Aquila.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Aquila.Services;

/// <summary>
/// PawnIO: the driver LibreHardwareMonitor reads the motherboard and the memory through.
///
/// Measured on 2026-10-14 — the same Aquila on the same machine, with and without it: without PawnIO the
/// motherboard reports nothing (37 sensors to 0: every fan and fan control, the board's temperatures, every
/// voltage), each DIMM nothing (21 to 0), and the CPU loses its CCD temperature. The library never installs it —
/// LibreHardwareMonitor's own app carries an installer and offers it — so a machine no other tool had brought
/// it to had half its readings missing, and no account of why.
///
/// Whether it is there is asked of the DRIVER, by opening its device the way the library does, never of the
/// registry: after PawnIO's uninstaller had run, the registry said "not installed" while the driver, loaded
/// until the next reboot, went on serving every reading.
///
/// The flow owns its dialogs, as the update's does: the offer at startup and the button in Settings are the same
/// install, and neither can leave half of it out.
/// </summary>
public sealed class PawnIoService(NoticeService notices, SettingsService settings, ILogger<PawnIoService> logger)
{
    private const string NoticeKey = "pawnio";
    private const string DialogTitle = "PawnIO";

    /// <summary>
    /// The newest official release, from PawnIO's own repository for them — the file its winget manifest installs.
    /// Fetched when needed rather than carried in Aquila's package: the driver is GPL-2.0, so carrying it would
    /// make Aquila a distributor of it, and a copy in the package would age with every release.
    /// </summary>
    private const string InstallerUrl =
        "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

    /// <summary>Who must have signed the installer, checked after Windows has found the signature valid: a valid
    /// signature by anyone else is not PawnIO.</summary>
    private const string Signer = "O=namazso";

    /// <summary>What goes missing without it, said once, in the words the notice, the question and Settings share.</summary>
    public const string WhatItReads = "fan speeds, the motherboard's temperatures and voltages, and the memory's temperatures";

    /// <summary>
    /// Whether the driver answers. Needs administrator rights, as reading through it does — so an unelevated Aquila
    /// cannot tell, and has a notice of its own about elevation.
    /// </summary>
    public static bool DriverAnswers()
    {
        using var device = CreateFile(@"\\?\GLOBALROOT\Device\PawnIO", 3 /* read and write data */,
            3 /* share read and write */, IntPtr.Zero, 3 /* open existing */, 0x80 /* normal */, IntPtr.Zero);
        return !device.IsInvalid;
    }

    /// <summary>
    /// Whether PawnIO's uninstall entry is there: a hint, readable without administrator rights, for the question
    /// the first run asks before elevating. Never the answer — that is <see cref="DriverAnswers"/>, which the entry
    /// has been seen to contradict.
    /// </summary>
    public static bool LooksInstalled()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var entry = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
            if (entry?.GetValue("DisplayVersion") is string)
                return true;
        }
        return false;
    }

    /// <summary>
    /// For the elevated helper of the first run: installs PawnIO when its driver does not answer, with no dialog —
    /// the helper has no window to show one in. A failure is logged and left to the elevated Aquila that follows,
    /// which finds the driver still missing and offers it again, saying why if it fails a second time.
    /// </summary>
    public async Task InstallQuietlyAsync()
    {
        if (DriverAnswers()) return;

        try
        {
            await InstallAsync();
            logger.LogInformation("PawnIO installed during first-run setup");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PawnIO was not installed during first-run setup");
        }
    }

    /// <summary>Raises the condition when the driver does not answer, withdraws it when it does, and says which.</summary>
    public bool Check()
    {
        if (DriverAnswers())
        {
            notices.Clear(NoticeKey);
            return true;
        }

        notices.Set(NoticeKey, "Fans and motherboard sensors are missing",
            $"Aquila reads {WhatItReads} through PawnIO, a driver that is not installed on this machine. "
            + "Settings → Diagnostics → PawnIO installs it.", StatusKind.Caution);
        return false;
    }

    /// <summary>
    /// At startup: the condition whenever the driver is missing, and the question the first time only. "Not now" is
    /// an answer — a question repeated at every start is one people learn to dismiss unread, and Settings keeps the
    /// button for later.
    /// </summary>
    public async Task OfferAtStartupAsync()
    {
        if (Check() || settings.Current.PawnIoOffered) return;

        settings.Current.PawnIoOffered = true;
        settings.Save();

        if (await Dialogs.Ask(DialogTitle,
                $"Aquila reads {WhatItReads} through PawnIO, a small signed driver that LibreHardwareMonitor uses. It is "
                + "not installed on this machine. Aquila can download it from its official release, check its signature "
                + "and install it.",
                yes: "Install PawnIO", no: "Not now"))
            await InstallAndRestartAsync();
    }

    /// <summary>
    /// Installs PawnIO and restarts Aquila, which is what reads through it: LibreHardwareMonitor opens its driver once,
    /// when it starts, so a driver installed under a running Aquila is seen only by the next one.
    /// </summary>
    public async Task InstallAndRestartAsync()
    {
        notices.Set(NoticeKey, "Installing PawnIO…", "Downloading the installer from its official release.", StatusKind.Plain);

        try
        {
            await InstallAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PawnIO was not installed");
            Check();
            await Dialogs.Tell(DialogTitle, ex.Message);
            return;
        }

        if (!Check())
        {
            await Dialogs.Tell(DialogTitle,
                "PawnIO's installer finished, but its driver does not answer yet. Restarting Windows usually settles it.");
            return;
        }

        logger.LogInformation("PawnIO installed; restarting to read through it");
        await Dialogs.Tell(DialogTitle, "PawnIO is installed. Aquila restarts now to read the sensors it opens up.");
        App.Restart();
    }

    /// <summary>Downloads, checks and runs PawnIO's installer. Throws with a sentence fit to show the user.</summary>
    private async Task InstallAsync()
    {
        if (!ElevationService.IsElevated())
            throw new InvalidOperationException("Installing PawnIO needs Aquila to run with administrator rights.");

        // A folder named afresh each time and writable by administrators only. The installer runs elevated, and in
        // the user's temp folder any program of the user's could swap the file between the signature check and the
        // run. The name cannot be guessed in advance, so nothing can be waiting there with an ACL of its own.
        var folder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "Aquila-PawnIO-" + Guid.NewGuid().ToString("N")));
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var who in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalSystemSid })
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(who, null), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        folder.Create(security);

        var installer = Path.Combine(folder.FullName, "PawnIO_setup.exe");
        try
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                await using var source = await http.GetStreamAsync(InstallerUrl);
                await using var target = File.Create(installer);
                await source.CopyToAsync(target);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new InvalidOperationException(
                    "PawnIO could not be downloaded. Check the internet connection and try again.", ex);
            }

            if (!SignedBy(installer, Signer))
                throw new InvalidOperationException(
                    "The downloaded installer is not signed by PawnIO's author, so it was not run.");

            // -install -silent: what PawnIO's own winget manifest runs.
            using var process = Process.Start(new ProcessStartInfo(installer, "-install -silent") { UseShellExecute = false })
                ?? throw new InvalidOperationException("PawnIO's installer did not start.");

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("PawnIO's installer did not finish within three minutes.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"PawnIO's installer failed (exit code {process.ExitCode}).");
        }
        finally
        {
            try { folder.Delete(recursive: true); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not remove {Folder}", folder.FullName); }
        }
    }

    /// <summary>
    /// Whether Windows finds the file's Authenticode signature valid — intact, and chained to a root it trusts — and
    /// the certificate it was signed with names <paramref name="subjectPart"/>. Revocation is not asked about: that is
    /// a round trip to the certificate authority on every install, for a certificate whose subject is checked anyway.
    /// </summary>
    private static bool SignedBy(string path, string subjectPart)
    {
        var fileInfo = new WinTrustFileInfo { Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = path };
        var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, filePointer, fDeleteOld: false);
            var data = new WinTrustData
            {
                Size = (uint)Marshal.SizeOf<WinTrustData>(),
                UIChoice = 2,           // no UI
                RevocationChecks = 0,   // none
                UnionChoice = 1,        // a file
                File = filePointer,
            };
            var genericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            if (WinVerifyTrust(IntPtr.Zero, genericVerifyV2, ref data) != 0)
                return false;
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }

        // SYSLIB0057 points at X509CertificateLoader, which reads certificate files and PKCS#12 — not the signer
        // of a signed program, which is what this asks for. The signature itself was judged by WinVerifyTrust above;
        // this only reads whose certificate it is.
#pragma warning disable SYSLIB0057
        using var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
        return signer.Subject.Contains(subjectPart, StringComparison.Ordinal);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, [MarshalAs(UnmanagedType.LPStruct)] Guid action,
        ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UIChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UIContext;
        public IntPtr SignatureSettings;
    }
}
