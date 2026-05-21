# GitHub Setup

## Release pipeline

Releases are triggered by pushing a version tag:

```
git tag v1.5.0
git push origin v1.5.0
```

The workflow (`.github/workflows/release.yml`) will:
1. Build with `dotnet publish -c Release -r win-x64`
2. Package with Velopack (`vpk pack`)
3. Sign the installer (if secrets are configured)
4. Create a GitHub Release with auto-generated notes and all artifacts

## Required secrets

Go to **Settings → Secrets and variables → Actions** and add:

| Secret | Value |
|---|---|
| `CERT_PFX_BASE64` | Base64-encoded PFX certificate file |
| `CERT_PASSWORD` | PFX certificate password |

To encode the certificate:
```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("aquila-cert.pfx")) | Set-Clipboard
```

If these secrets are absent the pipeline still runs but skips code signing.

## Local builds

Use `build.ps1` (gitignored, kept locally):

```powershell
# With signing (password from env var)
$env:AQUILA_PFX_PASSWORD = "your-password"
.\build.ps1 -Version 1.5.0

# Without signing
.\build.ps1 -Version 1.5.0
```

Artifacts are written to `.\releases\`.
