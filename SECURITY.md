# Security policy

## Reporting a vulnerability

Please report security issues **privately**, through GitHub:
[**Report a vulnerability**](https://github.com/JoaoCrv/Aquila/security/advisories/new).

Aquila runs elevated — LibreHardwareMonitor needs administrator rights to read temperatures and fan
speeds — so a vulnerability in it can matter more than in an ordinary desktop app. Please do not open a
public issue for one.

You can expect an acknowledgement within a week. Aquila is maintained by one person, so a fix may take
longer; you will be kept informed, and credited in the release notes unless you prefer otherwise.

## Supported versions

Only the latest release receives fixes. Installed copies update themselves, so the latest release is the
version almost everyone is running.

## Verifying a download

Releases are not code-signed. Each one is built by GitHub Actions from a public commit and carries a signed
build attestation, which you can check against the file you downloaded:

```
gh attestation verify Aquila-win-Setup.exe --repo JoaoCrv/Aquila
```

The same attestation is attached to each release as `Aquila-<version>.intoto.jsonl`. With it, the check does
not depend on GitHub's attestation service:

```
gh attestation verify Aquila-win-Setup.exe --repo JoaoCrv/Aquila --bundle Aquila-<version>.intoto.jsonl
```

The release notes also link a VirusTotal scan of the installer and of the portable build.
