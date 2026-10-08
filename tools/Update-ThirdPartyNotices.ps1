#Requires -Version 5.1
<#
.SYNOPSIS
    Writes THIRD-PARTY-NOTICES.txt from the packages Aquila ships, and refuses any licence not on the list.

.DESCRIPTION
    The installer carries 91 packages, and their licences ask for something in return: MIT and Apache-2.0 that
    their notice travels with the binaries, the MPL that the recipient is told where the source is. Nothing
    in the installer said any of it until this file existed.

    Generated, never written by hand, so it cannot fall behind a dependency update. The same run is the licence
    policy: a package whose licence is missing, unreadable or not in tools/licences/allowed.json fails it before
    anything is written. Corrections for packages whose metadata says nothing or says it badly live in
    tools/licences/overrides.json (by exact version, so an update makes someone look again) and
    url-mappings.json — each checked against the project's own licence file before it was added.

.PARAMETER Check
    Write nothing; fail if the committed file is not what this run would write. What CI and the release use, so
    a dependency that changed without the notices being regenerated stops the build instead of shipping.

.EXAMPLE
    .\tools\Update-ThirdPartyNotices.ps1           # after changing a package: regenerate, then commit the file
#>
param([switch]$Check)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$config = Join-Path $PSScriptRoot "licences"
$target = Join-Path $root "THIRD-PARTY-NOTICES.txt"
$report = Join-Path ([IO.Path]::GetTempPath()) "aquila-licences.json"

Push-Location $root
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

    dotnet restore Aquila.csproj -p:EnableWindowsTargeting=true | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    if (Test-Path $report) { Remove-Item $report }

    dotnet tool run nuget-license -i Aquila.csproj -t `
        -a (Join-Path $config "allowed.json") `
        -mapping (Join-Path $config "url-mappings.json") `
        -override (Join-Path $config "overrides.json") `
        -o JsonPretty -fo $report | Out-Null
    $toolExit = $LASTEXITCODE

    if (-not (Test-Path $report)) { throw "nuget-license wrote no report (exit code $toolExit)." }
    # Assigned first and wrapped after: Windows PowerShell's ConvertFrom-Json emits a JSON array as ONE object, so
    # @( ... | ConvertFrom-Json) would make a list of one list.
    $packages = Get-Content $report -Raw -Encoding UTF8 | ConvertFrom-Json
    $packages = @($packages)

    # The policy. Named one by one, because "3 validation errors" sends nobody anywhere.
    $refused = @($packages | Where-Object { $_.ValidationErrors -and @($_.ValidationErrors).Count -gt 0 })
    if ($refused.Count -gt 0 -or $toolExit -ne 0) {
        foreach ($p in $refused) {
            $why = (@($p.ValidationErrors) | ForEach-Object { $_.Error }) -join "; "
            Write-Host "  $($p.PackageId) $($p.PackageVersion) [$($p.License)]: $why" -ForegroundColor Red
        }
        throw "Licence check failed. A licence not in tools/licences/allowed.json needs a decision before it ships; " +
              "missing or wrong metadata needs an entry in tools/licences/overrides.json, checked against the " +
              "project's own licence file."
    }

    # Sorted ordinally, never by culture: the file must come out byte for byte the same on a Portuguese desktop
    # and an American runner, or the check below fails on nothing.
    $byName = [System.Comparison[object]] { param($a, $b) [string]::CompareOrdinal($a.PackageId, $b.PackageId) }
    $list = New-Object 'System.Collections.Generic.List[object]'
    foreach ($p in $packages) { $list.Add($p) }
    $list.Sort($byName)

    $licences = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in $list) { if (-not $licences.Contains($p.License)) { $licences.Add($p.License) } }
    $licences.Sort([StringComparer]::Ordinal)

    $rule = "-" * 79
    $text = New-Object System.Text.StringBuilder
    function Line([string]$s = "") { [void]$text.Append($s).Append("`n") }

    Line "Aquila - third-party notices"
    Line ("=" * 28)
    Line
    Line "Aquila is licensed under the Mozilla Public License 2.0 (see LICENSE.txt). It is built on the open-source"
    Line "packages below, which ship with it. Each is listed under its licence with its version, copyright and"
    Line "project page; the full text of every licence follows once, at the end. The source code of each package,"
    Line "including the MPL-2.0 ones, is available from its project page."
    Line
    Line "Generated from the packages themselves by tools/Update-ThirdPartyNotices.ps1 - do not edit by hand."

    foreach ($licence in $licences) {
        $members = @($list | Where-Object { $_.License -eq $licence })
        Line
        Line $rule
        Line "$licence ($($members.Count) package$(if ($members.Count -ne 1) { 's' }))"
        Line $rule
        foreach ($p in $members) {
            Line "$($p.PackageId) $($p.PackageVersion)"
            # Some nuspecs wrap their copyright across lines; one line reads the same and diffs cleanly.
            $copyright = if ($p.Copyright) { ($p.Copyright -replace '\s+', ' ').Trim() } else { "" }
            if ($copyright) { Line "  $copyright" }
            elseif ($p.Authors) { Line "  Authors: $(($p.Authors -replace '\s+', ' ').Trim())" }
            if ($p.PackageProjectUrl) { Line "  $($p.PackageProjectUrl)" }
        }
    }

    Line
    Line ("=" * 79)
    Line "LICENCE TEXTS"
    Line ("=" * 79)
    foreach ($licence in $licences) {
        $file = Join-Path $config "texts\$licence.txt"
        if (-not (Test-Path $file)) { throw "No text for $licence in tools/licences/texts - add the SPDX text." }
        Line
        Line "--- $licence ---"
        Line
        # The SPDX texts carry a "Copyright (c) <year> <holder>" template line. Each package's own copyright is
        # listed with it above, so the template line would only be a blank to fill in.
        $body = (Get-Content $file -Raw -Encoding UTF8) -replace "`r`n", "`n"
        $body = ($body -split "`n" | Where-Object { $_ -notmatch '^\s*Copyright \(c\) <year>' }) -join "`n"
        Line $body.Trim()
    }

    $generated = $text.ToString()

    if ($Check) {
        $committed = if (Test-Path $target) { (Get-Content $target -Raw -Encoding UTF8) -replace "`r`n", "`n" } else { "" }
        if ($committed -ne $generated) {
            throw "THIRD-PARTY-NOTICES.txt is out of date with the packages. Run tools/Update-ThirdPartyNotices.ps1 " +
                  "and commit the file."
        }
        Write-Host "Licences: $($list.Count) packages, all allowed; THIRD-PARTY-NOTICES.txt is current." -ForegroundColor Green
    }
    else {
        [IO.File]::WriteAllText($target, $generated, (New-Object System.Text.UTF8Encoding $false))
        Write-Host "Licences: $($list.Count) packages, all allowed; THIRD-PARTY-NOTICES.txt written." -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
