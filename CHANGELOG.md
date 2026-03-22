# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

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

