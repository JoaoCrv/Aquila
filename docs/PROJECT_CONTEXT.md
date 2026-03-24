# Aquila — Project Context

> Auto-generated context file for AI-assisted development.
> Last updated based on branch `Home_Explorer`, version **1.0.2**.

---

## 0. AI Assistant — Session Rules

> Rules established to avoid terminal lockups and wasted context budget.

- **No `run_build`** — builds are done manually with `Ctrl+Shift+B` in Visual Studio. If there are errors, the user pastes them into the chat.
- **No `git diff`** without a specific file path — generates excessive output that blocks the terminal.
- **No long-output terminal commands** — anything that may produce more than ~20 lines of output should be avoided.
- **No `&&` in PowerShell** — it is not a valid statement separator. Use separate commands or `git -C <path> <command>`.
- **Commits** are done via the user's external PowerShell or Visual Studio Source Control panel, not through the Copilot terminal.
- **Build verification** — after code changes, ask the user to build with `Ctrl+Shift+B` and report any errors.
- **Chat length** — long chats accumulate context and cause the terminal to hang on permission prompts. Start a new chat when the conversation becomes long, and re-read this file and `docs/ROADMAP.md` to restore context.

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

| Layer            | Technology                                                                              | Version  |
| ---------------- | --------------------------------------------------------------------------------------- | -------- |
| Runtime          | .NET 9 (Windows Desktop)                                                                | net9.0   |
| UI Framework     | WPF                                                                                     | —        |
| UI Component Kit | [WPF-UI (Lepo.co)](https://github.com/lepoco/wpfui)                                    | 4.0.2    |
| MVVM Toolkit     | CommunityToolkit.Mvvm                                                                   | 8.4.0    |
| DI / Hosting     | Microsoft.Extensions.Hosting                                                            | 9.0.1    |
| DI (WPF-UI)      | WPF-UI.DependencyInjection (page provider)                                              | 4.0.2    |
| Hardware Data    | [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.4    |
| Charts           | LiveChartsCore.SkiaSharpView.WPF                                                        | —        |
| Auto-Update      | [Velopack](https://velopack.io/)                                                        | 0.0.1298 |
| Versioning       | [Versionize](https://github.com/versionize/versionize) (global .NET tool)              | 2.4.0    |

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
   ???????????????? ??????????? ????????????????
   ?  Services    ? ? Models  ? ?  ViewModels  ?
   ???????????????? ??????????? ????????????????
         ?               ?              ?
         ?  ??????????????              ?
         ?  ?                           ?
   ???????????????????          ????????????????
   ? HardwareMonitor ????????????  Dashboard   ?
   ?    Service      ?          ?  ViewModel   ?
   ? (DispatcherTimer?          ????????????????
   ?  1s polling)    ?          ?  Explorer    ?
   ???????????????????          ?  ViewModel   ?
            ?                   ????????????????
            ?                           ?
   ???????????????????          ????????????????
   ?  ComputerData   ?          ?    Views     ?
   ?  (Model root)   ?          ? (XAML Pages) ?
   ?  ? HardwareList ?          ? DataContext =?
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

### Design Patterns in Use

| Pattern | Where | Purpose |
|---|---|---|
| **MVVM** | Views + ViewModels | Separation of UI from logic |
| **Dependency Injection** | `App.xaml.cs` ? all services and VMs | Loose coupling, testability |
| **Observer** | `HardwareMonitorService.DataUpdated` event | VMs react to data changes without polling |
| **Visitor** | `UpdateVisitor` (LHM) | Traverse hardware tree without modifying LHM classes |
| **Repository** (lightweight) | `SensorLocator` | Centralises all sensor lookup logic away from VMs |
| **Singleton** | All services and VMs via DI | Single instance shared across the app lifetime |

---

### Future Architecture — Phase 13 (Provider System)

The current tight coupling to LibreHardwareMonitor will be replaced by a clean provider abstraction.
See `docs/ROADMAP.md` Phase 13 for the full implementation plan.

```
????????????????????????????????????????????????????????????
?              DataAggregatorService                       ?
?  Owns polling timer — merges all providers into one      ?
?  SystemSnapshot — fires DataUpdated(SystemSnapshot)      ?
????????????????????????????????????????????????????????????
                       ? IDataProvider
      ????????????????????????????????????????????????????
      ?                ?                  ?              ?
 LhmProvider     WinApiProvider     AmdAdlProvider    MockProvider
 (LibreHardware  (Windows OS APIs   (future — AMD     (testing /
  Monitor)        no admin needed)   SDK / NVAPI)      UI dev)
```

**Design patterns in the provider system:**
