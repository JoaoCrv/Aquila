# Aquila — Development Roadmap

> Organized by priority. Each phase builds on the previous one.
> Linked to GitHub Issues at <https://github.com/JoaoCrv/Aquila/issues>

---

## GitHub Issues Tracker

| Issue | Title | Phase | Status |
| ----- | ----- | ----- | ------ |
| [#2](https://github.com/JoaoCrv/Aquila/issues/2) | Velopack Update — verificação manual de atualizações | Phase 2 (2.5) | ?? Open |
| [#3](https://github.com/JoaoCrv/Aquila/issues/3) | Adicionar SystemTray e AutoLaunch | Phase 7 (7.1–7.5) | ?? Open |
| [#4](https://github.com/JoaoCrv/Aquila/issues/4) | HardwareMonitorService deve arrancar na abertura da aplicação | Phase 1 (1.8) | ?? Open |
| [#5](https://github.com/JoaoCrv/Aquila/issues/5) | Dar possibilidade de copiar o identifier a partir do explorer | Phase 4 (4.7) | ?? Open |

---

## Phase 1 — Code Cleanup & Foundation (No New Features)

> Goal: Solid, clean codebase before adding complexity.

- [ ] **1.1** Remove legacy model files (`SensorInfo.cs`, `hardwareModel.cs`) — they are unused by the active architecture.
- [ ] **1.2** Remove or implement `AppConfig.cs` — currently defined but never used.
- [x] **1.3** Fix `OnPropertyChanged(nameof(_effectiveCpuClock))` ? `nameof(EffectiveCpuClock)` in `DashboardViewModel` — the `[ObservableProperty]` setter already notifies correctly, this call is wrong.
- [x] **1.4** Fix duplicate "CPU Speed" card in the GPU section of `DashboardPage.xaml`.
- [ ] **1.5** Implement `IDisposable` on `HardwareMonitorService` — call `Computer.Close()` and stop the timer on disposal. Hook it to app shutdown.
- [ ] **1.6** Fix `MainWindow.xaml.cs` — remove duplicate `GetNavigation()` override and unimplemented `SetServiceProvider` that throws.
- [ ] **1.7** Clean up unnecessary `using` statements across all files.
- [ ] **1.8** ?? [#4](https://github.com/JoaoCrv/Aquila/issues/4) — Optimize `HardwareMonitorService` startup: start monitoring immediately on app launch (background), so data is ready before the user navigates to any page. The `ExplorerPage` ProgressRing should only reflect the GroupBy processing time, not the initial hardware scan.
- [x] **1.9** **Add missing Network sensor properties to `DashboardViewModel`** — `NetworkUploadSpeedSensor`, `NetworkDownloadSpeedSensor`, `NetworkDataDownloadedSensor`, `NetworkDataUploadedSensor` added. Removed non-existent `NetworkUsageSensor` card from XAML.
- [x] **1.10** **Remove redundant `OnPropertyChanged` calls for sensor properties** — `DataSensor.Value` is already `[ObservableProperty]`, so XAML bindings to `.Value` update automatically. The `DataUpdated` handler only needs to call `OnPropertyChanged` for computed properties (`EffectiveCpuClock`) and string properties (`CpuName`, `GpuName`, `MemoryName`, `NetworkName`) that aren't observable on their own.

---

## Phase 2 — Dynamic Sensor Discovery & Updates (Critical for Portability)

> Goal: Make the Dashboard work on ANY computer, not just the developer's machine.

- [x] **2.1** Replace hardcoded sensor identifiers in `DashboardViewModel` with dynamic lookup via `SensorLocator`.
- [x] **2.2** Handle the case where a sensor is not yet available (first few ticks) — `SensorLocator` returns null, XAML `FallbackValue="--"` handles the display.
- [x] **2.3** GPU vendor detection — `SensorLocator.DetectGpuType()` checks AMD ? NVIDIA ? Intel in order.
- [x] **2.4** `SensorLocator` static helper created in `Helpers/SensorLocator.cs` — centralizes all sensor discovery logic with name-pattern fallbacks.
- [ ] **2.5** ?? [#2](https://github.com/JoaoCrv/Aquila/issues/2) — Velopack: replace auto-update on startup with a manual "Check for Updates" button in Settings. Avoid silent restarts and handle errors gracefully.

---

## Phase 3 — Storage Page

> Goal: Bring the `StoragePage` to life.

- [ ] **3.1** Create `StorageViewModel` that reads storage hardware from `ComputerData`.
- [ ] **3.2** Display per-drive info: name, type (SSD/HDD/NVMe), temperature, read/write rates, health (S.M.A.R.T. if available from LHM).
- [ ] **3.3** Show used/total space using `DriveInfo` from .NET for capacity data.
- [ ] **3.4** Design the UI with `ui:Card` widgets, progress bars for space usage.

---

## Phase 4 — UI/UX Improvements

> Goal: More polished and informative interface.

- [ ] **4.1** Add **mini-graphs/sparklines** to Dashboard cards showing last N seconds of sensor history.
- [ ] **4.2** Add **color indicators** — green/yellow/red based on thresholds (e.g., CPU temp > 80°C = red).
- [ ] **4.3** Make Dashboard **responsive** — adapt card layout to window size (use `UniformGrid` or `WrapPanel` with min/max widths).
- [ ] **4.4** Add **tooltips** on cards showing min/max values (already tracked by `DataSensor.Min` / `DataSensor.Max`).
- [ ] **4.5** Improve Explorer page — add search/filter, collapsible groups.
- [ ] **4.6** Add **animations** — smooth value transitions on cards.
- [ ] **4.7** ?? [#5](https://github.com/JoaoCrv/Aquila/issues/5) — Explorer: add a "Copy Identifier" button or right-click context menu on each sensor row, so users can easily copy sensor paths (useful for debugging and Dashboard configuration).
- [ ] **4.8** **Refactor Dashboard cards into a reusable `UserControl` or `DataTemplate`** — current XAML is 400+ lines of identical repeated structure. A `SensorCardControl` with `Title`, `Icon`, `Sensor` (DataSensor) and `ValueOverride` (for EffectiveCpuClock) properties would reduce markup to ~20 lines per section and make future changes (colors, sparklines) happen in one place.

---

## Phase 5 — Settings & Persistence

> Goal: User preferences that survive restarts.

- [ ] **5.1** Implement a settings file (JSON) for user preferences. Use `System.Text.Json`.
- [ ] **5.2** Persist theme choice.
- [ ] **5.3** Persist window position and size.
- [ ] **5.4** Allow user to configure **polling interval** (1s, 2s, 5s).
- [ ] **5.5** Allow user to choose which Dashboard cards to show/hide.

---

## Phase 6 — Logging & Error Handling

> Goal: Diagnosable issues, especially for hardware access problems.

- [ ] **6.1** Add `Microsoft.Extensions.Logging` with a file sink (Serilog or NLog).
- [ ] **6.2** Replace all `Console.WriteLine` / `Debug.WriteLine` with structured logging.
- [ ] **6.3** Add a log viewer page or export button in Settings.
- [ ] **6.4** Improve error handling in `HardwareMonitorService` — gracefully handle sensor read failures.

---

## Phase 7 — System Tray & Notifications

> Goal: Run in background, show alerts.

- [ ] **7.1** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) — Add system tray icon (WPF-UI `NotifyIcon` or `Hardcodet.NotifyIcon.Wpf`).
- [ ] **7.2** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) — Minimize to tray option (close button minimizes to tray instead of exiting).
- [ ] **7.3** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) — Start minimized / start with OS / start minimized with widgets.
- [ ] **7.4** Configurable alerts (e.g., notify when CPU temp > threshold).
- [ ] **7.5** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) — Tray context menu with quick actions (open, settings, exit).

---

## Phase 8 — Internationalization (i18n)

> Goal: Multi-language support.

- [ ] **8.1** Implement resource-based translations (`.resx` files or a JSON-based system).
- [ ] **8.2** Fill `Resources/Translations.cs` or replace with proper resource infrastructure.
- [ ] **8.3** Add language selector in Settings.
- [ ] **8.4** Start with English (en) and Portuguese (pt-PT).

---

## Phase 9 — Release Pipeline & CI/CD

> Goal: Automate and secure the build ? package ? publish workflow.

- [ ] **9.1** **Auto-read version from `.csproj`** — `build.ps1` should extract `<Version>` instead of prompting with `Read-Host`. Versionize already bumps it, so the script should just use it.
- [ ] **9.2** **Remove hardcoded PFX password** — read from environment variable (`$env:PFX_PASSWORD`) or a secrets manager. Never store passwords in scripts.
- [ ] **9.3** **Integrate Versionize into the script** — single command that runs `versionize` ? `dotnet publish` ? `vpk pack` in sequence.
- [ ] **9.4** **Auto-create GitHub Release** — after `vpk pack`, use `gh release create vX.Y.Z ./Releases/* --notes-file CHANGELOG.md` to publish the release automatically.
- [ ] **9.5** **Move script to `scripts/` folder** — organize build tooling (the folder already exists with an empty README).
- [ ] **9.6** **GitHub Actions CI** — automate build + test on push/PR. Consider automating release on tag push.
- [ ] **9.7** **Validate prerequisites** — script should check that `dotnet`, `vpk`, `versionize`, and `gh` are available before running.

---

## Phase 10 — Advanced Features (Future)

- [ ] **10.1** **Export data** — CSV/JSON export of sensor readings.
- [ ] **10.2** **Sensor history** — ring buffer of last N minutes, viewable in a detail page.
- [ ] **10.3** **Custom dashboard layouts** — drag-and-drop card arrangement.
- [ ] **10.4** **Network details page** — per-adapter breakdown.
- [ ] **10.5** **Process monitor** — top processes by CPU/Memory (using `System.Diagnostics.Process`).
- [ ] **10.6** **Remote monitoring** — lightweight web server or API to view stats from phone/other PC.

---

## Versioning Plan

| Version | Milestone                              |
| ------- | -------------------------------------- |
| 1.0.x   | Current state — basic monitoring       |
| 1.1.0   | Phase 1 + 2 complete (clean + portable)|
| 1.2.0   | Phase 3 (Storage page)                 |
| 1.3.0   | Phase 4 (UI polish)                    |
| 1.4.0   | Phase 5 (Settings persistence)         |
| 1.5.0   | Phase 6 (Logging)                      |
| 1.6.0   | Phase 9 (Release pipeline)             |
| 2.0.0   | Phase 7+ (Tray, i18n, advanced)        |
