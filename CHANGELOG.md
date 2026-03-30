# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="1.3.0"></a>
## [1.3.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.3.0) (2026-03-30)

### Features

* 4.3 responsive dashboard layout — WrapPanel + ColumnWidthConverter (3→2→1 col) ([079d70b](https://www.github.com/JoaoCrv/Aquila/commit/079d70b595dd2e5edd5bc53aeb390cd465ed546f))
* complete dashboard cards — Storage card, RAM/Power polish, SensorLocator storage helpers ([0c6c5cb](https://www.github.com/JoaoCrv/Aquila/commit/0c6c5cb9625e17fca065abddc9a35c20f2388d6d))
* Explorer Copy Identifier ([#5](https://www.github.com/JoaoCrv/Aquila/issues/5)/4.7), expand state memory (4.12), StoragePage tooltips (4.4) ([ab82001](https://www.github.com/JoaoCrv/Aquila/commit/ab820013751a6ff58269e55b559f1ac506220e2c))
* Explorer search/filter (4.5) ([2134350](https://www.github.com/JoaoCrv/Aquila/commit/2134350064338d84028b2841f2c494eee94cded4))
* GPU sparklines (4.1), network throughput chart (4.1), GPU card refactor (4.8) ([9c65d9e](https://www.github.com/JoaoCrv/Aquila/commit/9c65d9eed029bfe103eaf8b948c9a7706eb077d7))
* implement StoragePage with DriveInfo + LHM sensor support ([b1d854a](https://www.github.com/JoaoCrv/Aquila/commit/b1d854a63e5cdfeafb7815a865b7076a923dc9fc))
* RAM sparkline, 2-col layout, responsive NavView padding (4.18, 4.19) ([09d3478](https://www.github.com/JoaoCrv/Aquila/commit/09d347886ccdf9d7281b5821799aaaccb8872a94))
* redesign StoragePage with Radiograph-inspired CardExpander layout ([47a32f2](https://www.github.com/JoaoCrv/Aquila/commit/47a32f2e92383070f64504fab77e3c8dc9d71976))
* **dashboard:** add header with system uptime and date/time (ROADM ([92c8cad](https://www.github.com/JoaoCrv/Aquila/commit/92c8cad3ce3db3df9683e76ba2c3f641261e94fc))
* **dashboard:** hide zero-RPM fan rows with ZeroToCollapsedConverter ([3666b08](https://www.github.com/JoaoCrv/Aquila/commit/3666b080ad6e9cf0502d2d8d62cd59ac23ae5007))
* **explorer:** add HTML sensor export button ([0fef64e](https://www.github.com/JoaoCrv/Aquila/commit/0fef64eb536f5be07827dad0b5e8aba88703d61c))
* **fans:** dynamic Maximum via FanMaxConverter (ROADMAP 4.13 partial) ([97d8861](https://www.github.com/JoaoCrv/Aquila/commit/97d886152f16911ea53257bffb302de74580ffdc))
* **theme:** 3-layer theming system + semantic colour ([1e70800](https://www.github.com/JoaoCrv/Aquila/commit/1e7080002464f488fbcb743b973effdd63e197fc))
* **ui:** remove NavigationView BreadcrumbBar header ([60a22e5](https://www.github.com/JoaoCrv/Aquila/commit/60a22e58811cbcbbe6904550a546b2281e5d0cb3))

### Bug Fixes

* remove duplicate CPU temp chip and zero-value sensors from temperatures card ([c9bd246](https://www.github.com/JoaoCrv/Aquila/commit/c9bd2465b94b810261b3b253d69f8cf99b800cc0))
* replace PerformanceCounter with PDH native API for page-fault counters ([c2081b5](https://www.github.com/JoaoCrv/Aquila/commit/c2081b5cb5a2fa665e491065378754c9b6286e12))
* **cleanup:** implement IDisposable on DashboardViewModel and AccentBrush ([f9feeee](https://www.github.com/JoaoCrv/Aquila/commit/f9feeee966c80080fb97ec483f63a90c32d3d051))
* **cpu:** revert DashboardViewModel to LHM 0.9.4-safe sensor logic ([1b6c8ea](https://www.github.com/JoaoCrv/Aquila/commit/1b6c8ea9f335ed21c08c04d911fe9c4d8f32a1c7))
* **explorer:** restore Frame scroll, replace ListBox with ItemsControl ([0a9531e](https://www.github.com/JoaoCrv/Aquila/commit/0a9531ebad729a06e8a46045fac073910d89b12a))
* **explorer:** skip GroupedHardware rebuild on re-navigation if hardware unchanged ( ([0b5d956](https://www.github.com/JoaoCrv/Aquila/commit/0b5d95657a0429bc239ab4e3019228c24c979ca4))

<a name="1.2.0"></a>
## [1.2.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.2.0) (2026-03-28)

### Features

* **dashboard:** add GPU sparklines, VRAM bar, rounded progress bars and StatBox styles ([5c11410](https://www.github.com/JoaoCrv/Aquila/commit/5c11410c123cb97625b68182f6e817802cd28cac))

### Bug Fixes

* **dashboard:** remove GPU CartesianChart sparklines to fix NullReferenceException on navigation ([f16e55e](https://www.github.com/JoaoCrv/Aquila/commit/f16e55e08f7771d8ea0a62b4a0dc0a7353b0d618))

<a name="1.1.0"></a>
## [1.1.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.1.0) (2026-03-22)

### Features

* add SensorLocator for dynamic sensor discovery ([95c286c](https://www.github.com/JoaoCrv/Aquila/commit/95c286c38b78a17a9a161a7cf23c415072444a6a))
* Add UiService for UI state management ([1f4c0aa](https://www.github.com/JoaoCrv/Aquila/commit/1f4c0aae3435d7698afaf4678fefba30bb2e1d8c))
* redesign ExplorerPage with CardExpander and hardware icons ([fbf6e62](https://www.github.com/JoaoCrv/Aquila/commit/fbf6e62edcbbe67d119b2db718a195ffc88e1064))
* use ListBox for sensor rows in ExplorerPage, prepare Add Widget column ([335d186](https://www.github.com/JoaoCrv/Aquila/commit/335d186cf791ec4416c149727d91a659e4f002c3))
* **dashboard:** add CpuGaugeValue, GpuGaugeValue and RamGaugeValue observable properties ([2d8354b](https://www.github.com/JoaoCrv/Aquila/commit/2d8354b95e4880de4ef754e87ee52dc513bac0eb))

### Bug Fixes

* bubble mouse wheel events to parent ScrollViewer in ExplorerPage ([d6c2ab9](https://www.github.com/JoaoCrv/Aquila/commit/d6c2ab961039e462840d1d9c17bb67c61774423f))
* clean up DashboardViewModel and DashboardPage ([ea11e70](https://www.github.com/JoaoCrv/Aquila/commit/ea11e7053c8270dfcf4e862030172509986126b6))
* constrain category label by stretching inner layout ([3e3d5d8](https://www.github.com/JoaoCrv/Aquila/commit/3e3d5d8723f2b5059275503e237fe4c71d644400))
* constrain category label width by merging with column headers Grid ([509cab1](https://www.github.com/JoaoCrv/Aquila/commit/509cab13179f0c2a77c0ece08cc44e54df09b5f1))
* correct assembly name in app.manifest from UiDesktopApp1.app to Aquila.app ([b5fb5ae](https://www.github.com/JoaoCrv/Aquila/commit/b5fb5aef86eb48f6038b0d0873666aef80cc4970))
* prevent category label from wrapping in ExplorerPage ([b3e2ac5](https://www.github.com/JoaoCrv/Aquila/commit/b3e2ac599a41f496c190b29fc4ba035a7e47cf60))
* restore sensor bindings in DashboardViewModel ([d2b2594](https://www.github.com/JoaoCrv/Aquila/commit/d2b2594fff5feb3fd64d809143967ed11b9b87e8))
* use NavigationView native scroll in ExplorerPage ([4756870](https://www.github.com/JoaoCrv/Aquila/commit/4756870e2beb802a1e2bff79832c0ed9286360b4))
* **dashboard:** add ThroughputConverter to display network speed in B/s KB/s or MB/s ([01cf092](https://www.github.com/JoaoCrv/Aquila/commit/01cf092ee6443e836949ccca8ae456916c700aa7))
* **dashboard:** remove _sensorsResolved flag to allow late GPU and Network sensor discovery ([34046a8](https://www.github.com/JoaoCrv/Aquila/commit/34046a83c7dad5c40fe211b11749b1b7bbb8c1d0))
* **explorer:** implement INavigationAware and fix race condition in InitializeAsync ([b2ca5ca](https://www.github.com/JoaoCrv/Aquila/commit/b2ca5ca298dd382eb7cf5a2e607735aaf6daf682))

<a name="1.0.2"></a>
## [1.0.2](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.0.2) (2025-12-01)

<a name="1.0.1"></a>
## [1.0.1](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.0.1) (2025-11-23)

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.0.0) (2025-11-23)

### Features

* Added velopack for easy installation and automated updates ([df8434e](https://www.github.com/JoaoCrv/Aquila/commit/df8434e4a4ea3a9cc41796c6f7b4b5bd21e4950b))
* Enhance update logic and UI in Aquila application ([dc8d00d](https://www.github.com/JoaoCrv/Aquila/commit/dc8d00d437f2d41b879c2caf1512380f2dd2c70f))
* improve async app initialization and update handling ([87a8bcd](https://www.github.com/JoaoCrv/Aquila/commit/87a8bcdbbd8f7c6667999b2bb98c922c37bbc2b9))
* improve version handling and UI layout ([e2e980f](https://www.github.com/JoaoCrv/Aquila/commit/e2e980fcb5cc1f7d4943047338b722dc29259e31))

### Breaking Changes

* Replaces the project base with the official Wpf.Ui template" -m "This change removes the corrupted .csproj configuration and adopts the recommended Dependency Injection and Host structure, ensuring compatibility with the UI library. ([c26f779](https://www.github.com/JoaoCrv/Aquila/commit/c26f779e68b0b400f0306b06ad96a0f4fc3e434e))

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.0.0) (2025-11-23)

### Features

* Added velopack for easy installation and automated updates ([df8434e](https://www.github.com/JoaoCrv/Aquila/commit/df8434e4a4ea3a9cc41796c6f7b4b5bd21e4950b))
* Enhance update logic and UI in Aquila application ([dc8d00d](https://www.github.com/JoaoCrv/Aquila/commit/dc8d00d437f2d41b879c2caf1512380f2dd2c70f))
* improve async app initialization and update handling ([87a8bcd](https://www.github.com/JoaoCrv/Aquila/commit/87a8bcdbbd8f7c6667999b2bb98c922c37bbc2b9))
* improve version handling and UI layout ([e2e980f](https://www.github.com/JoaoCrv/Aquila/commit/e2e980fcb5cc1f7d4943047338b722dc29259e31))

### Breaking Changes

* Replaces the project base with the official Wpf.Ui template" -m "This change removes the corrupted .csproj configuration and adopts the recommended Dependency Injection and Host structure, ensuring compatibility with the UI library. ([c26f779](https://www.github.com/JoaoCrv/Aquila/commit/c26f779e68b0b400f0306b06ad96a0f4fc3e434e))

