# Aquila Ч Development Roadmap

> Organized by priority. Each phase builds on the previous one.
> Linked to GitHub Issues at <https://github.com/JoaoCrv/Aquila/issues>

---

## GitHub Issues Tracker

| Issue | Title | Phase | Status |
| ----- | ----- | ----- | ------ |
| [#2](https://github.com/JoaoCrv/Aquila/issues/2) | Velopack Update Ч verificaчуo manual de atualizaчїes | Phase 2 (2.5) | ?? Open |
| [#3](https://github.com/JoaoCrv/Aquila/issues/3) | Adicionar SystemTray e AutoLaunch | Phase 7 (7.1Ц7.5) | ?? Open |
| [#4](https://github.com/JoaoCrv/Aquila/issues/4) | HardwareMonitorService deve arrancar na abertura da aplicaчуo | Phase 1 (1.8) | ?? Open |
| [#5](https://github.com/JoaoCrv/Aquila/issues/5) | Dar possibilidade de copiar o identifier a partir do explorer | Phase 4 (4.7) | ?? Open |

---

## Phase 0 Ч Identified Bugs & Code Problems (Untracked)

> Bugs and code problems found during codebase analysis that were not previously tracked in any GitHub issue or roadmap phase.
> Should be resolved before or alongside Phase 1.

### Bugs

- [x] **0.1 (BUG)** `ExplorerPage` does not reload when navigating back to it Ч fixed by implementing `INavigationAware.OnNavigatedToAsync()` which calls `InitializeAsync()` on every navigation.
  - **File:** `Views/Pages/ExplorerPage.xaml.cs`

- [x] **0.2 (BUG)** Race condition in `ExplorerViewModel.InitializeAsync()` Ч fixed by snapshotting `HardwareList` and each `hw.Sensors` collection on the UI thread before passing to `Task.Run`.
  - **File:** `ViewModels/Pages/ExplorerViewModel.cs`

- [x] **0.3 (BUG)** `_sensorsResolved` flag prevents late GPU and Network sensor discovery Ч removed the flag entirely; `NotifySensorReferences()` is now called unconditionally every tick.
  - **File:** `ViewModels/Pages/DashboardViewModel.cs`

- [x] **0.4 (BUG)** `ExplorerPage.xaml` has no `ScrollViewer` Ч the page root sets `ScrollViewer.VerticalScrollBarVisibility="Disabled"` and the `ItemsControl` also disables scroll, with no wrapping `ScrollViewer`. With several hardware sections expanded, content is clipped at the window boundary. Fixed by restoring `ScrollViewer.VerticalScrollBarVisibility="Auto"` on the `Page` element Ч the WPF `Frame` (used by WPF-UI NavigationView) has a built-in `ScrollViewer` controlled by this attached property. No custom `ScrollViewer` wrapper needed. Also replaced inner `ListBox` with `ItemsControl` to eliminate the `ListBox`'s internal `ScrollViewer` that captured `MouseWheel` events.
  - **File:** `Views/Pages/ExplorerPage.xaml`

- [x] **0.5 (BUG)** `app.manifest` has the wrong assembly name (`UiDesktopApp1.app`) Ч corrected to `Aquila.app`.
  - **File:** `app.manifest`

- [x] **0.6 (BUG)** `ExplorerViewModel.InitializeAsync()` rebuilds the entire `GroupedHardware` list on every navigation Ч fixed by caching a hardware signature (`IReadOnlyList<(string Name, HardwareType Type)>`) and returning early in `InitializeAsync()` when `GroupedHardware` is already populated and the signature matches. First navigation always runs; hot-plug hardware triggers a rebuild on the next navigation.
  - **File:** `ViewModels/Pages/ExplorerViewModel.cs`

- [ ] **0.7 (BUG)** `DashboardViewModel` subscribes to `ApplicationThemeManager.Changed` but never unsubscribes Ч causes a memory leak if the ViewModel is ever re-created (currently a singleton, so low impact, but incorrect regardless).
  - **File:** `ViewModels/Pages/DashboardViewModel.cs`

- [ ] **0.8 (BUG)** `AccentBrushProvider` subscribes to `ApplicationThemeManager.Changed` but never unsubscribes Ч same issue as 0.7. Both should unsubscribe in a `Dispose()` or by using a `WeakEventManager`.
  - **File:** `Helpers/AccentBrushProvider.cs`

### Code Quality

- [x] **0.9 (CODE)** `ExplorerPage` and `AboutPage` do not implement `INavigableView<TViewModel>` Ч both now implement the WPF-UI contract, consistent with `DashboardPage` and `SettingsPage`.
  - **Files:** `Views/Pages/ExplorerPage.xaml.cs`, `Views/Pages/AboutPage.xaml.cs`

- [x] **0.10 (CODE)** Network throughput displayed as raw `B/s` Ч added `ThroughputConverter` (`Helpers/ThroughputConverter.cs`) that scales B/s ? KB/s ? MB/s. Applied to Download and Upload labels in `DashboardPage.xaml`.
  - **Files:** `Helpers/ThroughputConverter.cs`, `Views/Pages/DashboardPage.xaml`

- [x] **0.11 (CODE)** `using HidSharp.Reports.Units` in `HardwareMonitorModel.cs` is unused Ч removed.
  - **File:** `Models/HardwareMonitorModel.cs`

- [x] **0.12 (CODE)** `Resources/Translations.cs` is a dead empty file Ч deleted.
  - **File:** `Resources/Translations.cs`

- [x] **0.13 (CODE)** `ExplorerPage.xaml.cs` has incorrect indentation Ч fixed, class body is now correctly indented.
  - **File:** `Views/Pages/ExplorerPage.xaml.cs`

- [x] **0.14 (CODE)** `NotifySensorReferences()` in `DashboardViewModel` calls `OnPropertyChanged` for sensor properties every tick Ч unnecessary since `DataSensor` is `ObservableObject` with `[ObservableProperty]` on `Value`. Fixed: added `_prev*` cache fields for all 13 sensor references and 3 string properties; `NotifyIfChanged<T>` generic helper skips notify when `Equals(last, current)`; only 6 derived non-observable values (RamTotalGb, PageReads/Writes, CacheGb, bar weights) notify every tick.
  - **File:** `ViewModels/Pages/DashboardViewModel.cs`

- [x] **0.15 (CODE)** `ProgressBarWidthConverter` receives `double` values but `DataSensor.Value` is `float` Ч the converter silently returns `0` when `values[0]` is a `float` because the `is not double` guard fails. Fixed by adding a `ToDouble()` helper with a switch expression that accepts `double`, `float`, and `int`.
  - **Files:** `Helpers/ProgressBarWidthConverter.cs`, `Views/Pages/DashboardPage.xaml`

---

## Phase 1 Ч Code Cleanup & Foundation (No New Features)

> Goal: Solid, clean codebase before adding complexity.

- [x] **1.1** Remove legacy model files (`SensorInfo.cs`, `hardwareModel.cs`) Ч confirmed already absent from the codebase.
- [x] **1.2** Remove or implement `AppConfig.cs` Ч confirmed already absent from the codebase.
- [x] **1.3** Fix `OnPropertyChanged(nameof(_effectiveCpuClock))` ? `nameof(EffectiveCpuClock)` in `DashboardViewModel` Ч the `[ObservableProperty]` setter already notifies correctly, this call is wrong.
- [x] **1.4** Fix duplicate "CPU Speed" card in the GPU section of `DashboardPage.xaml`.
- [x] **1.5** Implement `IDisposable` on `HardwareMonitorService` Ч `Computer.Close()` and timer stop on disposal. `ApplicationHostService.StopAsync` calls `Dispose()`.
- [x] **1.6** Fix `MainWindow.xaml.cs` Ч duplicate `GetNavigation()` removed; `SetServiceProvider` now correctly no-ops instead of throwing.
- [x] **1.7** Clean up unnecessary `using` statements Ч removed `using HidSharp.Reports.Units` (0.11), removed unused imports in `ExplorerPage.xaml.cs`.
- [x] **1.8** ?? [#4](https://github.com/JoaoCrv/Aquila/issues/4) Ч `HardwareMonitorService.StartMonitoring()` is called in `ApplicationHostService.StartAsync` before the window opens Ч data is ready on first navigation.
- [x] **1.9** Fix `ProgressBarWidthConverter` to accept both `float` and `double` inputs (see 0.15) Ч affects the RAM segmented bar and all Dashboard progress bars.
- [x] **1.10** Implement `IDisposable` on `DashboardViewModel` Ч unsubscribes from `_monitorService.DataUpdated` and `ApplicationThemeManager.Changed` in `Dispose()`. DI container calls `Dispose()` automatically during `_host.Dispose()` on app exit (see 0.7).
- [x] **1.11** Implement `IDisposable` on `AccentBrushProvider` Ч unsubscribes from `ApplicationThemeManager.Changed` in `Dispose()`. Not in DI (XAML resource); `App.OnExit` calls `(Resources["AccentBrushes"] as IDisposable)?.Dispose()` explicitly before `_host.StopAsync()`. Removed dead `static Instance` property (zero references, was creating a redundant second subscriber) (see 0.8).

---

## Phase 2 Ч Dynamic Sensor Discovery & Updates (Critical for Portability)

> Goal: Make the Dashboard work on ANY computer, not just the developer's machine.

- [x] **2.1** Replace hardcoded sensor identifiers in `DashboardViewModel` with dynamic lookup via `SensorLocator`.
- [x] **2.2** Handle the case where a sensor is not yet available (first few ticks) Ч `SensorLocator` returns null, XAML `FallbackValue="--"` handles the display.
- [x] **2.3** GPU vendor detection Ч `SensorLocator.DetectGpuType()` checks AMD ? NVIDIA ? Intel in order.
- [x] **2.4** `SensorLocator` static helper created in `Helpers/SensorLocator.cs` Ч centralizes all sensor discovery logic with name-pattern fallbacks.
- [ ] **2.5** ?? [#2](https://github.com/JoaoCrv/Aquila/issues/2) Ч Velopack: replace auto-update on startup with a manual "Check for Updates" button in Settings. Avoid silent restarts and handle errors gracefully.
- [ ] **2.6** Multi-network adapter support Ч `SensorLocator` currently returns the first network adapter only. When multiple adapters exist (e.g. Wi-Fi + Ethernet), the Dashboard should show the active one or allow selection. `AllNetworkAdapters()` helper needed.
- [ ] **2.7** Handle hot-plug hardware Ч if a USB device or external GPU is connected after app launch, the `HardwareMonitorService` won't pick it up without a restart. Evaluate whether LHM supports re-opening or if a periodic re-scan is needed.

---

## Phase 3 Ч Storage Page

> Goal: Bring the `StoragePage` to life.

- [ ] **3.1** Create `StorageViewModel` that reads storage hardware from `ComputerData`.
- [ ] **3.2** Display per-drive info: name, type (SSD/HDD/NVMe), temperature, read/write rates, health (S.M.A.R.T. if available from LHM).
- [ ] **3.3** Show used/total space using `DriveInfo` from .NET for capacity data.
- [ ] **3.4** Design the UI with `ui:Card` widgets, progress bars for space usage.
- [ ] **3.5** Add read/write throughput sparklines (reuse the `LineSeries<double>` + ring buffer pattern from `DashboardViewModel`).
- [ ] **3.6** Show drive letter and filesystem type alongside capacity Ч `DriveInfo.DriveFormat` (NTFS, exFAT, etc.).

---

## Phase 4 Ч UI/UX Improvements

> Goal: More polished and informative interface.

- [ ] **4.1** Add **mini-graphs/sparklines** to Dashboard cards showing last N seconds of sensor history.
- [x] **4.2** Add **color indicators** Ч green/yellow/red based on thresholds. Implemented as a full 3-layer theming system: `Themes/Aquila.Default.xaml` (palette + semantic tokens Dark/Light), `Styles/SensorStyles.xaml` (ThinBar + named bars `CpuBar`/`GpuBar`/`RamBar`/`TempBar`/`FanBar`/`PowerBar`), `Helpers/ThermalBrushConverter.cs` (green < 50 ░C ? orange 50Ц79 ░C ? red ? 80 ░C). `AccentBrushProvider` now publishes `{DynamicResource Aquila.*}` keys for light/dark auto-switching. All 34 hardcoded `AccentBrushes` bindings in `DashboardPage.xaml` replaced. Thermal converter applied to all temperature values and bars.
- [ ] **4.3** Make Dashboard **responsive** Ч adapt card layout to window size (use `UniformGrid` or `WrapPanel` with min/max widths).
- [ ] **4.4** Add **tooltips** on cards showing min/max values (already tracked by `DataSensor.Min` / `DataSensor.Max`).
- [ ] **4.5** Improve Explorer page Ч add search/filter, collapsible groups.
- [ ] **4.6** Add **animations** Ч smooth value transitions on cards.
- [ ] **4.7** ?? [#5](https://github.com/JoaoCrv/Aquila/issues/5) Ч Explorer: add a "Copy Identifier" button or right-click context menu on each sensor row, so users can easily copy sensor paths (useful for debugging and Dashboard configuration).
- [ ] **4.8** **Refactor Dashboard cards into a reusable `UserControl` or `DataTemplate`** Ч current XAML is 400+ lines of identical repeated structure. A `SensorCardControl` with `Title`, `Icon`, `Sensor` (DataSensor) and `ValueOverride` (for EffectiveCpuClock) properties would reduce markup to ~20 lines per section and make future changes (colors, sparklines) happen in one place.
- [x] **4.9** Fix `ExplorerPage` scroll clipping (see 0.4) Ч resolved via 0.4.
- [x] **4.10** Added a **Dashboard header** showing total system uptime and current date/time Ч two chips right-aligned above the card grid. `SystemUptime` uses `Environment.TickCount64`; `CurrentDateTime` uses `DateTime.Now`. A `DispatcherTimer` (1-minute interval, `DispatcherPriority.Background`) notifies both properties; timer is stopped in `DashboardViewModel.Dispose()`.
- [x] **4.11** **CPU summary subtitle** on the CPU card Ч `BuildCpuSummary()` returns core/thread count (e.g. `"8 Cores"`); wired to a `TextBlock` below the CPU name with `NullToCollapsedConverter` visibility. Clock display omitted due to LHM 0.9.4 Zen 5 bug (deferred to LHM upgrade ROADMAP item).
- [ ] **4.12** Explorer: remember last expanded/collapsed state of each `CardExpander` per session Ч store a `HashSet<string>` of expanded hardware identifiers in `ExplorerViewModel` and restore on re-navigation.
- [ ] **4.13** **Fan Card v2 Ч Control % + dynamic Maximum** Ч LHM exposes both `SensorType.Fan` (RPM) and `SensorType.Control` (%) for each fan header with matching `Index`. Use `Control %` (0Ц100) for the `ProgressBar` (eliminates the need for a hardcoded `Maximum="3000"`), keep RPM as the label value. Requires pairing `(FanSensor, ControlSensor)` by index in `SensorLocator` or `DashboardViewModel`. Related to item C (dynamic Maximum).
  - **Partial (item C done):** `FanMaxConverter` binds `ProgressBar.Maximum` to `DataSensor.Max` with a guard for `Max=0` (fallback 3000). Full Control % pairing deferred until own hardware API is in place Ч the API will expose a `FanReading` model with `Rpm`, `Percent` and `MaxRpm` already paired, eliminating the need for the converter entirely.
- [ ] **4.14** **Typography & spacing tokens in `Aquila.Default.xaml`** Ч extend the 3-layer theming system to cover non-colour design properties: `Aquila.FontSize.Label`, `Aquila.FontSize.Value`, `Aquila.FontSize.Chip`, `Aquila.FontWeight.Value`, `Aquila.Spacing.Row`, `Aquila.Spacing.Section`, etc. Styles in `SensorStyles.xaml` would consume these tokens so a single change in the theme file propagates to every card.
- [x] **4.15** **Dashboard card polish** Ч RAM label changed from `"MEM╙RIA RAM"` to `"RAM"` for consistency with other section labels. GPU2 row added to the Power card (hidden via `NullToCollapsedConverter` when a second GPU is absent). Network card Received/Sent labels remain in place. `ZeroToCollapsedConverter` generalized to handle `int` (for `StorageCards.Count`) in addition to `float`/`double`.
- [x] **4.16** **Storage card on Dashboard** Ч new `ui:Card` in Column 1 (below Network) showing per-drive data via `ItemsControl`. Each drive shows: name chip, Temp + Used% stat boxes, ? Read / ? Write rate rows with progress bars (max 600 MB/s). Data backed by `StorageDriveData` (mirrors `GpuCardData` pattern) and `SensorLocator` helpers (`AllStorageDrives`, `StorageTemperatureFor`, `StorageReadRateFor`, `StorageWriteRateFor`, `StorageUsedSpaceFor`). Card collapses when no storage hardware is detected. Read/write labels reuse `ThroughputConverter` (B/s ? KB/s ? MB/s).

---

## Phase 5 Ч Settings & Persistence

> Goal: User preferences that survive restarts.

- [ ] **5.1** Implement a settings file (JSON) for user preferences. Use `System.Text.Json`.
- [ ] **5.2** Persist theme choice.
- [ ] **5.3** Persist window position and size.
- [ ] **5.4** Allow user to configure **polling interval** (1s, 2s, 5s).
- [ ] **5.5** Allow user to choose which Dashboard cards to show/hide.
- [ ] **5.6** Persist Explorer expanded/collapsed state (links to 4.12) Ч save the `HashSet<string>` to the settings JSON file so it survives restarts.
- [ ] **5.7** Allow user to configure **network adapter** used in the Dashboard Network card (links to 2.6).

---

## Phase 6 Ч Logging & Error Handling

> Goal: Diagnosable issues, especially for hardware access problems.

- [ ] **6.1** Add `Microsoft.Extensions.Logging` with a file sink (Serilog or NLog).
- [ ] **6.2** Replace all `Console.WriteLine` / `Debug.WriteLine` with structured logging.
- [ ] **6.3** Add a log viewer page or export button in Settings.
- [ ] **6.4** Improve error handling in `HardwareMonitorService` Ч gracefully handle sensor read failures.
- [ ] **6.5** Surface hardware access errors to the user Ч if `Computer.Open()` fails (e.g. missing admin rights), show a clear message in the UI instead of silently showing `--` everywhere.
- [ ] **6.6** Add crash reporting Ч catch unhandled exceptions in `App.OnDispatcherUnhandledException` (already wired, currently empty) and write a crash log to `%AppData%\Aquila\crash.log`.

---

## Phase 7 Ч System Tray & Notifications

> Goal: Run in background, show alerts.

- [ ] **7.1** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) Ч Add system tray icon (WPF-UI `NotifyIcon` or `Hardcodet.NotifyIcon.Wpf`).
- [ ] **7.2** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) Ч Minimize to tray option (close button minimizes to tray instead of exiting).
- [ ] **7.3** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) Ч Start minimized / start with OS / start minimized with widgets.
- [ ] **7.4** Configurable alerts (e.g., notify when CPU temp > threshold).
- [ ] **7.5** ?? [#3](https://github.com/JoaoCrv/Aquila/issues/3) Ч Tray context menu with quick actions (open, settings, exit).
- [ ] **7.6** Tray tooltip showing a summary of current stats (CPU%, GPU%, RAM%) Ч updated every polling tick via `HardwareMonitorService.DataUpdated`.

---

## Phase 8 Ч Internationalization (i18n)

> Goal: Multi-language support.

- [ ] **8.1** Implement resource-based translations (`.resx` files or a JSON-based system).
- [ ] **8.2** Fill `Resources/Translations.cs` or replace with proper resource infrastructure.
- [ ] **8.3** Add language selector in Settings.
- [ ] **8.4** Start with English (en) and Portuguese (pt-PT).

---

## Phase 9 Ч Release Pipeline & CI/CD

> Goal: Automate and secure the build ? package ? publish workflow.

- [ ] **9.1** **Auto-read version from `.csproj`** Ч `build.ps1` should extract `<Version>` instead of prompting with `Read-Host`. Versionize already bumps it, so the script should just use it.
- [ ] **9.2** **Remove hardcoded PFX password** Ч read from environment variable (`$env:PFX_PASSWORD`) or a secrets manager. Never store passwords in scripts.
- [ ] **9.3** **Integrate Versionize into the script** Ч single command that runs `versionize` ? `dotnet publish` ? `vpk pack` in sequence.
- [ ] **9.4** **Auto-create GitHub Release** Ч after `vpk pack`, use `gh release create vX.Y.Z ./Releases/* --notes-file CHANGELOG.md` to publish the release automatically.
- [ ] **9.5** **Move script to `scripts/` folder** Ч organize build tooling (the folder already exists with an empty README).
- [ ] **9.6** **GitHub Actions CI** Ч automate build + test on push/PR. Consider automating release on tag push.
- [ ] **9.7** **Validate prerequisites** Ч script should check that `dotnet`, `vpk`, `versionize`, and `gh` are available before running.

---

## Phase 10 Ч Advanced Features (Future)

- [ ] **10.1** **Export data** Ч CSV/JSON export of sensor readings.
- [ ] **10.2** **Sensor history** Ч ring buffer of last N minutes, viewable in a detail page.
- [ ] **10.3** **Custom dashboard layouts** Ч drag-and-drop card arrangement.
- [ ] **10.4** **Network details page** Ч per-adapter breakdown.
- [ ] **10.5** **Process monitor** Ч top processes by CPU/Memory (using `System.Diagnostics.Process`).
- [ ] **10.6** **Remote monitoring** Ч lightweight web server or API to view stats from phone/other PC.

---

## Phase 11 Ч Desktop Widgets (Rainmeter-style)

> Goal: Allow users to pin any sensor from the Explorer as a floating widget on the desktop.
> The Explorer page is the natural starting point Ч every sensor is already listed and one click should be enough to create a widget.

### Concept
- Each `DataSensor` can be "pinned" as a transparent, always-on-top `Window` that floats over the desktop (like Rainmeter skins).
- Widgets are fully configurable: position, size, style, update rate.
- The Explorer page acts as the **widget catalogue** Ч the user browses sensors and clicks "Add Widget" on any row.

### Tasks
- [ ] **11.1** Design `WidgetDefinition` model Ч stores sensor identifier, widget type (text, bar, graph), position, size, theme.
- [ ] **11.2** Persist widget definitions to a JSON file in `AppData` (reuse Settings infrastructure from Phase 5).
- [ ] **11.3** Create `WidgetWindow` Ч a transparent, borderless, always-on-top `Window` that binds to a `DataSensor` via `HardwareMonitorService`.
- [ ] **11.4** Add "Add Widget" button to each sensor row in `ExplorerPage` Ч opens a `WidgetConfigDialog` to choose widget style before pinning.
- [ ] **11.5** ?? [#5](https://github.com/JoaoCrv/Aquila/issues/5) Ч "Copy Identifier" also lives here, useful for manual widget config.
- [ ] **11.6** `WidgetManagerService` Ч singleton that tracks all active `WidgetWindow` instances, loads/saves definitions, and connects each widget to the live sensor data stream.
- [ ] **11.7** Widget styles (start simple, expand over time):
  - `TextWidget` Ч large value + label (e.g. "72 ░C / CPU Package")
  - `BarWidget` Ч horizontal/vertical fill bar (e.g. CPU load %)
  - `GraphWidget` Ч sparkline of last N seconds (links to Phase 4.1)
- [ ] **11.8** Widget tray menu Ч right-click on tray icon (Phase 7) shows active widgets with toggle visibility / remove options.
- [ ] **11.9** Lock/unlock widget positions Ч drag to reposition when unlocked, fixed when locked.

---

## Phase 12 Ч Performance & Memory

> Goal: Keep CPU and memory overhead as low as possible, especially during long-running sessions.

- [ ] **12.1** **Reduce per-tick allocations in `DashboardViewModel`** Ч `OnDataUpdated` allocates new `List<CoreBarItem>`, `List<GpuCardData>`, `List<LabelledSensor>`, and `List<DataSensor>` on every tick (once per second). These should be updated in-place or pooled to avoid GC pressure over long sessions.
- [ ] **12.2** **Profile memory growth over time** Ч run the app for 30+ minutes and verify that memory stays flat. Identify any unbounded collections or event subscriptions that grow with uptime.
- [ ] **12.3** **Reduce unnecessary `OnPropertyChanged` calls** Ч `NotifySensorReferences()` fires for every sensor property on every tick (see 0.14). Evaluate which properties actually change after first load and skip notifications for stable references.
- [ ] **12.4** **Throttle UI updates when the window is minimized** Ч if the main window is minimized (and tray is not active), reduce the polling interval or skip UI notifications entirely to save CPU. `HardwareMonitorService` can still poll at full rate for tray tooltip accuracy.
- [ ] **12.5** **LiveCharts animation budget** Ч each chart has `AnimationsSpeed = 200ms`. With multiple charts running simultaneously, this can cause frame drops on slower machines. Make the animation speed configurable (links to Phase 5) or disable it below a performance threshold.
- [ ] **12.6** **`PerformanceCounter` warmup delay** Ч the first call to `NextValue()` always returns 0 and is discarded in `StartMonitoring()`. However, `PageReadsPerSec` and `PageWritesPerSec` still show 0 on the first tick because the second call happens immediately. Add a 1-tick skip on first use or show `--` until the first valid reading.

---

## Phase 13 Ч Provider Architecture (Data Source Abstraction)

> Goal: Decouple the app's data model from any specific hardware library or OS API.
> Replace the current tight coupling to `LibreHardwareMonitor` with a clean provider system
> that supports multiple simultaneous data sources and is extensible by third-party contributors.

### Design Patterns

This phase implements a combination of well-established patterns:

| Pattern | Role |
|---|---|
| **Adapter** (GoF) | Each `IDataProvider` adapts an external API (LHM, AMD ADL, NVAPI) to the app's internal model |
| **Aggregator** | `DataAggregatorService` merges multiple independent sources into one `SystemSnapshot` |
| **Strategy** | Active providers are swappable at runtime via Settings without changing consumers |
| **Chain of Responsibility** | Providers ordered by priority Ч first valid value for a field wins |
| **Null Object** | `MockProvider` Ч inert implementation for testing and UI development without hardware |
| **Plugin / Open-Closed** | `IEnumerable<IDataProvider>` via .NET DI Ч new providers added without touching existing code |

The .NET DI container supports multiple registrations of the same interface natively Ч
`services.AddSingleton<IDataProvider, LhmProvider>()` repeated per provider,
resolved automatically via `IEnumerable<IDataProvider>` in the aggregator constructor.

### Motivation

The current architecture has `HardwareMonitorService` serving two distinct roles:
1. **LHM translator** Ч polls LibreHardwareMonitor and maps raw sensors to `ComputerData`.
2. **OS metrics collector** Ч reads Windows `PerformanceCounter` APIs directly.

As the app grows, more data sources will be needed: Windows OS APIs for memory/network/storage,
manufacturer SDKs (AMD ADL, NVAPI, ASUS AURA, MSI SDK), and potentially remote agents.
Mixing all of these into one service is unsustainable. This phase introduces a proper abstraction.

### Target Architecture

```
???????????????????????????????????????????????????????????????????
?                    DataAggregatorService                        ?
?  - Owns the polling timer (1s default, configurable)            ?
?  - Discovers and manages active IDataProvider instances         ?
?  - Merges provider outputs into a single SystemSnapshot         ?
?  - Fires DataUpdated(SystemSnapshot) Ч single event for all VMs ?
???????????????????????????????????????????????????????????????????
                            ? IDataProvider
       ??????????????????????????????????????????????
       ?                    ?                       ?
 LhmProvider         WinApiProvider          (future)
 LibreHardwareMonitor Windows OS APIs        AmdAdlProvider
 ? CPU, GPU temps     ? RAM capacity         NvApiProvider
 ? fan speeds         ? page file            AsusAuraProvider
 ? power draw         ? file cache           MsiSdkProvider
 ? storage S.M.A.R.T  ? network stats        RemoteAgentProvider
 ? voltages           ? drive capacity       MockProvider (testing)
                      ? process list
                      ? system uptime
```

### Canonical Data Model (`SystemSnapshot`)

The app's internal "API" Ч strongly typed, independent of any external library.
ViewModels bind to `SystemSnapshot` properties directly, with no `SensorLocator.Find()` calls.

```
SystemSnapshot
??? Cpu       : CpuSnapshot
?   ??? Name, Cores, Threads, MaxClockMhz
?   ??? LoadPercent, TemperatureCelsius, PowerWatts, EffectiveClockMhz
?   ??? CoreLoads : float[]
??? Gpus      : List<GpuSnapshot>
?   ??? Name, Vendor
?   ??? LoadPercent, TemperatureCelsius, PowerWatts, ClockMhz
?   ??? VramUsedMb, VramTotalMb
?   ??? Fans : float[]
??? Memory    : MemorySnapshot
?   ??? UsedGb, AvailableGb, TotalGb, LoadPercent
?   ??? CacheGb, PageReadsPerSec, PageWritesPerSec
?   ??? VirtualUsedGb, VirtualAvailableGb
??? Network   : List<NetworkAdapterSnapshot>
?   ??? Name, IsActive
?   ??? DownloadBytesPerSec, UploadBytesPerSec
?   ??? TotalDownloadedGb, TotalUploadedGb
??? Storage   : List<DriveSnapshot>
?   ??? Name, Type (SSD/HDD/NVMe), DriveLetter, FileSystem
?   ??? UsedGb, TotalGb, ReadBytesPerSec, WriteBytesPerSec
?   ??? TemperatureCelsius, HealthPercent (S.M.A.R.T.)
?   ??? IsSystemDrive
??? System    : SystemInfoSnapshot
?   ??? UptimeSeconds, CurrentDateTime
?   ??? OsVersion, MachineName
??? Temperatures : List<LabelledValue>   (all system temps for the Temperatures card)
    Fans         : List<LabelledValue>   (all system fans for the Fans card)
```

### `IDataProvider` Interface

```csharp
public interface IDataProvider : IDisposable
{
    string   Name        { get; }   // "LibreHardwareMonitor 0.9.4"
    bool     IsAvailable { get; }   // auto-detected on startup
    int      Priority    { get; }   // lower = preferred when conflict
    void     Initialize();
    void     Update(SystemSnapshot snapshot);
}
```

Rules:
- Each provider **fills only the fields it owns** Ч it does not clear fields set by other providers.
- If a provider cannot read a value, it leaves the snapshot field unchanged (previous tick value stays).
- **Priority** resolves conflicts: if both LHM and AMD ADL report GPU temperature, the lower priority number wins.

### Tasks

- [ ] **13.1** Define `SystemSnapshot` and all sub-snapshot classes in `Models/SystemSnapshot.cs`. Keep them as plain C# records or classes with no external dependencies. This is the app's canonical data contract.

- [ ] **13.2** Define `IDataProvider` interface in `Services/Providers/IDataProvider.cs`.

- [ ] **13.3** Create `WinApiProvider` in `Services/Providers/WinApiProvider.cs`:
  - RAM: `GlobalMemoryStatusEx` (total, available, load%, virtual) Ч replaces LHM memory sensors for capacity data.
  - Cache: `PerformanceCounter("Memory", "Cache Bytes")` Ч already implemented in `HardwareMonitorService`.
  - Page activity: `PerformanceCounter("Memory", "Page Reads/sec")` and `Page Writes/sec`.
  - Network: `PerformanceCounter("Network Interface", ...)` per adapter Ч replaces LHM network sensors.
  - Storage capacity: `DriveInfo` Ч replaces the planned `DriveInfo` calls in Phase 3.
  - System uptime: `Environment.TickCount64`.
  - Does **not** require admin privileges.

- [ ] **13.4** Create `LhmProvider` in `Services/Providers/LhmProvider.cs` Ч migrate the current `HardwareMonitorService` LHM logic into this provider:
  - CPU: load, temperature, power, per-core loads, clocks, fans.
  - GPU: load, temperature, power, clock, VRAM, fans.
  - Memory: temperature, power (the parts only LHM can provide).
  - Storage: S.M.A.R.T. health, temperature, read/write rates.
  - Motherboard: temperature sensors, fan speeds.
  - Requires admin privileges (`Computer.Open()`). Gracefully degrades if unavailable.

- [ ] **13.5** Create `DataAggregatorService` in `Services/DataAggregatorService.cs`:
  - Owns the `DispatcherTimer` (currently in `HardwareMonitorService`).
  - On each tick: calls `provider.Update(snapshot)` for each active provider in priority order.
  - Fires `DataUpdated(SystemSnapshot snapshot)`.
  - Exposes `IReadOnlyList<IDataProvider> ActiveProviders` for the Settings page.

- [ ] **13.6** Update `DashboardViewModel` to consume `SystemSnapshot` directly Ч remove all `SensorLocator` calls, remove the `ComputerData` reference, bind to strongly typed snapshot properties. `OnDataUpdated(SystemSnapshot snap)` replaces the current parameterless event handler.

- [ ] **13.7** Update `ExplorerViewModel` to use `SystemSnapshot` or keep a raw sensor tree from `LhmProvider` Ч the Explorer page benefits from the full hierarchical sensor list, so `LhmProvider` should optionally expose its raw `ComputerData` alongside the snapshot.

- [ ] **13.8** Register providers and `DataAggregatorService` in `App.xaml.cs` DI Ч providers as transient or singleton depending on lifecycle, aggregator as singleton.

- [ ] **13.9** Add provider selection to Settings (Phase 5) Ч let the user enable/disable individual providers and set priority order. Persisted to the settings JSON file.

- [ ] **13.10** Create `MockProvider` in `Services/Providers/MockProvider.cs` Ч fills `SystemSnapshot` with realistic static values. Used for UI development without physical hardware and for future unit tests.

- [ ] **13.11** Document the provider contract in `docs/PROVIDER_CONTRACT.md` Ч spec for third-party contributors wanting to build a provider plugin (AMD ADL, NVAPI, ASUS AURA, MSI SDK, etc.).

### Migration Strategy

This is a breaking refactor. Recommended approach to avoid destabilising the app mid-development:

1. Build `SystemSnapshot` + `IDataProvider` + `WinApiProvider` alongside the existing code (no removals yet).
2. Build `DataAggregatorService` that runs both old and new in parallel temporarily.
3. Migrate `DashboardViewModel` to `SystemSnapshot` Ч validate Dashboard still works.
4. Migrate `ExplorerViewModel`.
5. Remove `HardwareMonitorService` and `SensorLocator` once all consumers are migrated.
6. `LhmProvider` is the last step Ч it is a straight extraction of the current LHM logic.

---

## Versioning Plan

| Version | Milestone                                         |
| ------- | ------------------------------------------------- |
| 1.0.x   | Current state Ч basic monitoring                  |
| 1.1.0   | Phase 0 bugs + Phase 1 cleanup                    |
| 1.2.0   | Phase 2 complete (portable sensor discovery)      |
| 1.3.0   | Phase 3 (Storage page)                            |
| 1.4.0   | Phase 4 (UI polish)                               |
| 1.5.0   | Phase 5 (Settings persistence)                    |
| 1.6.0   | Phase 6 (Logging & error handling)                |
| 1.7.0   | Phase 9 (Release pipeline)                        |
| 2.0.0   | Phase 7 + 8 (Tray, i18n)                         |
| 2.1.0   | Phase 12 (Performance & memory)                   |
| 3.0.0   | Phase 13 (Provider architecture Ч breaking refactor) |
| 3.1.0   | Phase 11 (Desktop Widgets Ч built on top of Phase 13) |
