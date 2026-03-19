# Aquila — Project Context

> Auto-generated context file for AI-assisted development.
> Last updated based on branch `Home_Explorer`, version **1.0.2**.

---

## 1. What Is Aquila?

Aquila is an **open-source desktop hardware monitoring application** built for Windows.
Its primary use case is displaying real-time system metrics (CPU, GPU, RAM, Network, Storage) on a secondary screen in a clean, modern UI.

- **License:** MPL 2.0
- **Author:** [@JoaoCrv](https://github.com/JoaoCrv)
- **Repository:** <https://github.com/JoaoCrv/Aquila>
- **Branch:** `Home_Explorer`
- **GitHub CLI:** ? Available — use `gh` commands for issue management
- **Open Issues:** 4 ([view all](https://github.com/JoaoCrv/Aquila/issues))
- **Labels:** `bug`, `enhancement`, `documentation`, `good first issue`, `help wanted`, `question`

---

## 2. Technology Stack

| Layer            | Technology                                                     | Version  |
| ---------------- | -------------------------------------------------------------- | -------- |
| Runtime          | .NET 9 (Windows Desktop)                                       | net9.0   |
| UI Framework     | WPF                                                            | —        |
| UI Component Kit | [WPF-UI (Lepo.co)](https://github.com/lepoco/wpfui)           | 4.0.2    |
| MVVM Toolkit     | CommunityToolkit.Mvvm                                          | 8.4.0    |
| DI / Hosting     | Microsoft.Extensions.Hosting                                   | 9.0.1    |
| DI (WPF-UI)     | WPF-UI.DependencyInjection (page provider)                     | 4.0.2    |
| Hardware Data    | [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.4 |
| Auto-Update      | [Velopack](https://velopack.io/)                               | 0.0.1298 |
| Versioning       | [Versionize](https://github.com/versionize/versionize) (global .NET tool) | 2.4.0 |

---

## 3. Architecture Overview

```
???????????????????????????????????????????????????????????
?  App.xaml.cs  (Composition Root)                        ?
?  ??? Microsoft.Extensions.Hosting (Generic Host)        ?
?  ??? DI container registrations                         ?
?  ??? Velopack auto-update bootstrap                     ?
???????????????????????????????????????????????????????????
                         ?
          ???????????????????????????????
          ?              ?              ?
   ?????????????? ????????????? ????????????????
   ?  Services  ? ?   Models  ? ?  ViewModels  ?
   ?????????????? ????????????? ????????????????
         ?               ?              ?
         ?  ??????????????              ?
         ?  ?                           ?
   ???????????????????          ????????????????
   ? HardwareMonitor ????????????  Dashboard   ?
   ?    Service       ?          ?  ViewModel   ?
   ? (DispatcherTimer ?          ????????????????
   ?  1s polling)     ?          ?  Explorer    ?
   ????????????????????          ?  ViewModel   ?
            ?                    ????????????????
            ?                           ?
   ???????????????????          ????????????????
   ?  ComputerData   ?          ?    Views     ?
   ?  (Model root)   ?          ? (XAML Pages) ?
   ?  ? HardwareList ?          ? DataContext = ?
   ?  ? SensorIndex  ?          ?   this       ?
   ???????????????????          ? ViewModel =  ?
                                ?   injected   ?
                                ????????????????
```

### Pattern: MVVM with Service Layer

- **Views** own their `ViewModel` via constructor injection and expose it as a property.
- `DataContext = this` on each page; XAML binds via `ViewModel.PropertyName`.
- **ViewModels** use `ObservableObject` (CommunityToolkit.Mvvm) with `[ObservableProperty]` and `[RelayCommand]` source generators.
- **Services** are registered as singletons in the DI container.

---

## 4. Key Services

### 4.1 `HardwareMonitorService`
- Wraps `LibreHardwareMonitor.Hardware.Computer`.
- Opens the computer with all categories enabled (CPU, GPU, Memory, Motherboard, Storage, Network).
- Uses a `DispatcherTimer` (1 second interval) to poll sensor data on the UI thread.
- Builds and maintains a `ComputerData` model:
  - `HardwareList` — `ObservableCollection<DataHardware>` (hierarchical, for Explorer).
  - `SensorIndex` — `Dictionary<string, DataSensor>` (flat lookup by identifier, for Dashboard widgets).
- Fires `DataUpdated` event each tick so ViewModels can refresh.

### 4.2 `ApplicationHostService`
- `IHostedService` — starts monitoring and opens the main window on `StartAsync`.

### 4.3 `UiService`
- Tiny observable singleton with `IsLoading` flag, used for a global loading overlay.

---

## 5. Data Model

```
ComputerData
??? HardwareList : ObservableCollection<DataHardware>
?   ??? DataHardware
?       ??? Identifier, Name, HardwareType
?       ??? Sensors : ObservableCollection<DataSensor>
??? SensorIndex : Dictionary<string, DataSensor>
    ??? DataSensor : ObservableObject
        ??? Index, Identifier, Name, SensorType, Unit
        ??? Value, Min, Max  (observable)
```

> **Legacy models still present** (not actively used by the new architecture):
> - `SensorInfo` — plain DTO with `ToString()` override.
> - `SensorModel` / `HardwareModel` — older observable model with manual `OnPropertyChanged`.
> These can be cleaned up in a future pass.

---

## 6. Pages & Navigation

| Page           | ViewModel              | Status          | Description                                     |
| -------------- | ---------------------- | --------------- | ----------------------------------------------- |
| DashboardPage  | DashboardViewModel     | ? Functional   | Cards with real-time CPU/GPU/RAM/Network stats.  |
| ExplorerPage   | ExplorerViewModel      | ? Functional   | Grouped tree of all sensors (all hardware).       |
| StoragePage    | —                      | ?? Placeholder  | Static text only. No ViewModel.                  |
| AboutPage      | AboutViewModel         | ? Functional   | Shows assembly version.                          |
| SettingsPage   | SettingsViewModel      | ? Functional   | Theme toggle (Light/Dark) with WPF-UI.           |

Navigation is handled by **WPF-UI's `NavigationView`** with `LeftFluent` pane mode, Mica backdrop, and a breadcrumb header.

---

## 7. How Each Package Is Used

### LibreHardwareMonitorLib 0.9.4
- `Computer` class — opened with all subsystems enabled.
- `IVisitor` pattern (`UpdateVisitor`) — traverses and updates hardware.
- `IHardware.Sensors` and `SubHardware` — iterated to build the flat + hierarchical model.
- `SensorType` enum — used for grouping and unit mapping.

### WPF-UI 4.0.2
- `FluentWindow` — main window with `Mica` backdrop, round corners, extends into title bar.
- `NavigationView` (LeftFluent) — sidebar navigation with icons (`SymbolRegular`).
- `TitleBar` — custom title bar with icon.
- `BreadcrumbBar` — page breadcrumbs.
- `SnackbarPresenter` — snackbar slot (not yet used).
- `Card` — dashboard widget containers.
- `ListView` / `GridView` — Explorer table.
- `ProgressRing` — global loading overlay.
- `ThemesDictionary` / `ControlsDictionary` — resource dictionaries for theming.
- `ApplicationThemeManager` / `SystemThemeWatcher` — runtime theme switching.

### WPF-UI.DependencyInjection 4.0.2
- `AddNavigationViewPageProvider()` — auto-resolves pages from DI for `NavigationView`.

### CommunityToolkit.Mvvm 8.4.0
- `ObservableObject` — base class for all ViewModels and `DataSensor`.
- `[ObservableProperty]` — source-generated bindable properties.
- `[RelayCommand]` — source-generated `ICommand` implementations.
- Used via `global using` in `Usings.cs`.

### Microsoft.Extensions.Hosting 9.0.1
- `Host.CreateDefaultBuilder()` — application host with DI, configuration.
- `IHostedService` — `ApplicationHostService` lifecycle.
- `IServiceProvider` — static access via `App.Services`.

### Velopack 0.0.1298
- `VelopackApp.Build().Run()` — early startup hook in `App` constructor.
- `UpdateManager` + `GithubSource` — checks GitHub releases for updates, downloads, and applies with restart.
- **Velopack CLI (`vpk`)** — used in `build.ps1` to package the app into an installer (`vpk pack`).

### Versionize 2.4.0 (global .NET tool)
- Installed globally (`dotnet tool install -g versionize`).
- Follows [Conventional Commits](https://www.conventionalcommits.org/) — commit messages like `feat:`, `fix:`, `refactor:`, `BREAKING CHANGE:` drive automatic versioning.
- Running `versionize` bumps the `<Version>` in `Aquila.csproj`, generates/appends `CHANGELOG.md`, and creates a git tag.
- Current version: **1.0.2** (set in `Aquila.csproj`).

### Release Pipeline (`build.ps1`) — git-ignored
Current manual release script. Steps:
1. `dotnet publish -c Release -r win-x64` — self-contained publish.
2. Validates publish directory and PFX certificate exist.
3. Prompts for version number manually (`Read-Host`).
4. `vpk pack` — packages with Velopack CLI, signs with code-signing certificate (`aquila-cert.pfx`, also git-ignored).
5. Signing uses SHA-256 with Comodo timestamp server.

**Known issues with the current script:**
- Version is entered manually — should be read from `Aquila.csproj` (Versionize already bumps it).
- PFX password is hardcoded in plain text — should use a secure vault or environment variable.
- No integration with Versionize — the two tools run independently.
- No automatic GitHub Release creation — releases must be uploaded manually.

---

## 8. Project Structure

```
Aquila/
??? App.xaml / App.xaml.cs          # Composition root, DI, Velopack
??? Aquila.csproj                   # Project file (.NET 9, WPF)
??? Usings.cs                       # Global usings
??? AssemblyInfo.cs                 # WPF ThemeInfo
??? app.manifest                    # UAC (asInvoker), DPI awareness
??? CHANGELOG.md                    # Auto-generated by Versionize
?
??? Assets/
?   ??? icon.ico
?   ??? Images/icon.png
?
??? docs/                           # Public documentation (tracked by git)
?   ??? ROADMAP.md                  # Development roadmap with GitHub issue links
?   ??? PROJECT_CONTEXT.md          # Architecture, stack, and project overview
?
??? drafts/                         # Private working files (git-ignored)
?   ??? DEVELOPMENT_INSTRUCTIONS.md # Coding conventions and AI assistant context
?
??? Helpers/
?   ??? EnumToBooleanConverter.cs   # IValueConverter for radio buttons ? enum
?
??? Extensions/
?   ??? TaskExtensions.cs           # SafeFireAndForget async helper
?
??? Models/
?   ??? HardwareMonitorModel.cs     # DataSensor, DataHardware, ComputerData (ACTIVE)
?   ??? SensorInfo.cs               # Legacy DTO
?   ??? hardwareModel.cs            # Legacy observable model (SensorModel, HardwareModel)
?   ??? AppConfig.cs                # Config placeholder (unused)
?
??? Services/
?   ??? HardwareMonitorService.cs   # Core hardware polling service
?   ??? ApplicationHostService.cs   # IHostedService startup
?   ??? UiService.cs                # Global loading state
?
??? ViewModels/
?   ??? Windows/
?   ?   ??? MainWindowViewModel.cs  # Nav items, title, loading relay
?   ??? Pages/
?       ??? DashboardViewModel.cs   # Sensor bindings, effective CPU clock calc
?       ??? ExplorerViewModel.cs    # Grouped hardware list builder
?       ??? SettingsViewModel.cs    # Theme switching
?       ??? AboutViewModel.cs       # Version display
?
??? Views/
?   ??? Windows/
?   ?   ??? MainWindow.xaml(.cs)    # FluentWindow + NavigationView
?   ??? Pages/
?       ??? DashboardPage.xaml(.cs) # Real-time widgets
?       ??? ExplorerPage.xaml(.cs)  # All sensors table
?       ??? StoragePage.xaml(.cs)   # Placeholder
?       ??? SettingsPage.xaml(.cs)  # Theme toggle
?       ??? AboutPage.xaml(.cs)     # About info
?
??? Styles/
?   ??? PageStyles.xaml             # Base page style (foreground + background)
?
??? Resources/
?   ??? Translations.cs             # Empty placeholder for future i18n
?
??? .github/                        # Issue templates, setup docs
```

---

## 9. Known Issues & Technical Debt

1. **Hardcoded sensor identifiers** in `DashboardViewModel` — these are specific to the developer's machine (AMD CPU, AMD GPU, NCT6687D motherboard sensor chip). Will not work on other systems.
2. **Legacy model files** — `SensorInfo.cs` and `hardwareModel.cs` are no longer used by the active architecture but still in the project.
3. **`AppConfig.cs`** — defined but never populated or used.
4. **`Translations.cs`** — empty placeholder.
5. **`StoragePage`** — no ViewModel, no data. Just a placeholder.
6. **`MainWindow.xaml.cs`** has duplicate `INavigationView GetNavigation()` and unimplemented `SetServiceProvider` — inherited from interface but throws `NotImplementedException`.
7. **Dashboard XAML** has a "CPU Speed" card duplicated in the GPU section (copy-paste leftover).
8. **No `IDisposable`** on `HardwareMonitorService` — `Computer.Close()` is never called on app exit.
9. **No logging framework** — only `Console.WriteLine` and `Debug.WriteLine`.
10. **`OnPropertyChanged(nameof(_effectiveCpuClock))`** — should be `nameof(EffectiveCpuClock)` (the public property, not the backing field).
11. **Network sensors missing in `DashboardViewModel`** — `DashboardPage.xaml` binds to `NetworkUsageSensor`, `NetworkUploadSpeedSensor`, `NetworkDownloadSpeedSensor`, `NetworkDataDownloadedSensor`, `NetworkDataUploadedSensor` — none of these properties exist in the ViewModel. All Network cards silently show `--`.
12. **`OnPropertyChanged` on sensors is redundant** — `DataSensor` is already `ObservableObject` with `[ObservableProperty]` on `Value`. The binding to `.Value` in XAML is already reactive. Notifying the parent sensor property (`OnPropertyChanged(nameof(CpuTemperatureSensor))`) every second is unnecessary — only needed if the sensor reference itself changes (which only happens once on first load).
13. **Dashboard XAML is 400+ lines of repetitive markup** — every card has identical structure. A reusable `DataTemplate` or `UserControl` for cards would reduce this to a fraction of the current size.
