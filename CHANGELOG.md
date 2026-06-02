# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="2.0.0"></a>
## [2.0.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v2.0.0) (2026-06-02)

### Features

* add initial hardware semantic snapshot ([b1a980a](https://www.github.com/JoaoCrv/Aquila/commit/b1a980aad6c118f7751aeb89a00a7105fd3c0c91))
* add sensor identifier tracking and enhance explorer view ([8e51941](https://www.github.com/JoaoCrv/Aquila/commit/8e51941ef2577f6e18e1475922179fec9fdb9fec))
* improve dashboard RAM card, DIMM sensors, and GPU fan visibility ([c9994fe](https://www.github.com/JoaoCrv/Aquila/commit/c9994fe083ca6ef1e866578d8500ce0ceb89416d))
* **ci:** automated release pipeline via GitHub Actions ([f818c92](https://www.github.com/JoaoCrv/Aquila/commit/f818c92b805ccff9aa6b90436f56ced3dfff65f1))
* **config:** add appsettings system with local override support ([14b8f9f](https://www.github.com/JoaoCrv/Aquila/commit/14b8f9f55b44785dc9605c936d6a46d159b4dcae))
* **dashboard:** add borderless dashboard window (Issue [#16](https://www.github.com/JoaoCrv/Aquila/issues/16)) ([05dc0ed](https://www.github.com/JoaoCrv/Aquila/commit/05dc0edad4435205e074dfa2589928ad63c94238))
* **dashboard:** add exit and close buttons in dashboard mode ([1ad056a](https://www.github.com/JoaoCrv/Aquila/commit/1ad056ad4ac9ee4756d12ecfc393886d6a80b607))
* **dashboard:** expandable sparklines with conditional visibility ([2b0ccbb](https://www.github.com/JoaoCrv/Aquila/commit/2b0ccbb7ac33ef372d95bf84d040e9626686c3ca))
* **dashboard:** fan card duty cycle and dynamic progress bar ([bb39c3b](https://www.github.com/JoaoCrv/Aquila/commit/bb39c3bb699e9cd9fc78dcd5601818d8081eb480))
* **dashboard:** fix RAM name display, remove cache, add virtual memory ([760298a](https://www.github.com/JoaoCrv/Aquila/commit/760298a8ffcc6d6faaf375f730aa7c7c9e77b902))
* **dashboard:** individual storage cards with I/O sparklines ([3de5238](https://www.github.com/JoaoCrv/Aquila/commit/3de52380edd45c1e5f15f47512f1a32a7f6714c1))
* **dashboard:** maximise to current screen on enable, restore on disable ([9bcc77b](https://www.github.com/JoaoCrv/Aquila/commit/9bcc77bafb5ee7cbdfa421cdd20e90fe1292a407))
* **dashboard:** persist card visibility across sessions ([cac7350](https://www.github.com/JoaoCrv/Aquila/commit/cac73500035610d7c06ebe791ec20b15cd7c7ed4))
* **dashboard:** polish dashboard window UX ([cac2673](https://www.github.com/JoaoCrv/Aquila/commit/cac26737ef9bd36ef3dc4ac4a230d6ce1f99256e))
* **dashboard:** polish toggle button and settings sync ([f46bf17](https://www.github.com/JoaoCrv/Aquila/commit/f46bf17c01bfcf2f8ab80adf3c66f54d2d067777))
* **dashboard:** responsive card grid with equal-height rows ([e252f59](https://www.github.com/JoaoCrv/Aquila/commit/e252f597e8edabf2bbe5bddd47de3ae33013d0a7))
* **logging:** add Serilog file logging with verbose toggle ([1014c93](https://www.github.com/JoaoCrv/Aquila/commit/1014c939280b08455b0a279ad0caf9a49d6dc792))
* **sensors:** record cpu temperature, total power and cpu fan history ([61fa949](https://www.github.com/JoaoCrv/Aquila/commit/61fa949b18de460f862dbc9bec8f4a08134d6e84))
* **settings:** add persistent polling interval with ComboBox ([4d6a91a](https://www.github.com/JoaoCrv/Aquila/commit/4d6a91a937ca628db978f97ab1d41a7cf349bf84))
* **settings:** Phase 5 — persistent settings with theme and path infrastructure ([eefa50d](https://www.github.com/JoaoCrv/Aquila/commit/eefa50d790f29db7c61b7f15cc5b6b6c7f159e4b))
* **storage:** add sparklines, health stats, and package updates ([05e13c1](https://www.github.com/JoaoCrv/Aquila/commit/05e13c1dd23a315304a0cd82f28e3971cbfb8caa))
* **theme:** add typography tokens for page and card section headers ([e87656b](https://www.github.com/JoaoCrv/Aquila/commit/e87656b59ed66752c770aa627a4c14bdaf76f111))
* **tray:** Phase 7 — system tray, minimize to tray, start minimized, start with Windows ([67205de](https://www.github.com/JoaoCrv/Aquila/commit/67205de5ef408e50825334c4e677a583c842ef0a))
* **tray:** window bounds persistence and dashboard mode ([d9c80d1](https://www.github.com/JoaoCrv/Aquila/commit/d9c80d1f459efd61ffc962d01a7fe341ed90f97c))

### Bug Fixes

* correct sensor mapping and dashboard bindings ([ddf7f9e](https://www.github.com/JoaoCrv/Aquila/commit/ddf7f9e4abedbdfc7e713a15aea8778357d760ea))
* **dashboard:** save window bounds on hide, not only on close ([7f6b3ae](https://www.github.com/JoaoCrv/Aquila/commit/7f6b3ae240bbcccf7e3b66093ec32329ebbc1861))
* **dashboard:** stable dashboard mode without PaneDisplayMode switching ([a1c58da](https://www.github.com/JoaoCrv/Aquila/commit/a1c58dabc9ca5d915fc0ba45914901c9a1158a07))
* **layout:** balanced page margins for normal and dashboard modes ([a233dfc](https://www.github.com/JoaoCrv/Aquila/commit/a233dfcf49a679c94d8ecc2630edfdb4e3a8ae32))

### Breaking Changes

* simplify hardware driver architecture with direct AquilaService integration ([5fde0bd](https://www.github.com/JoaoCrv/Aquila/commit/5fde0bd81ba8b9fc42bdd9a93a84033e83963b3a))

<a name="1.4.0"></a>
## [1.4.0](https://www.github.com/JoaoCrv/Aquila/releases/tag/v1.4.0) (2026-04-04)

### Features

* add manual update notifications ([f5a9d55](https://www.github.com/JoaoCrv/Aquila/commit/f5a9d5558495b18b9d0947554b01e3eef18995e4))
* improve dashboard layout for 1080p ([18a27b7](https://www.github.com/JoaoCrv/Aquila/commit/18a27b793466ddd35e3b923f1f330540849d903a))
* merge ui-phase1 — Dashboard/Explorer/StoragePage + SparklineChart ([624d3fa](https://www.github.com/JoaoCrv/Aquila/commit/624d3fa5345943f165d04129e5e653aa336c0bda))

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

