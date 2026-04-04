
# Aquila

Aquila is a free and open-source Windows hardware monitoring app built with .NET and LibreHardwareMonitor. It is designed for secondary screens and focuses on presenting CPU, GPU, RAM, network, and storage metrics in a clean WPF dashboard.

## Project principles

- free to use
- open source under `MPL-2.0`
- no ads
- no telemetry or personal data collection
- optional network communication only for update checks and downloads through `Velopack`

## Maintainer

- [@JoaoCrv](https://github.com/JoaoCrv)

## Privacy

Aquila does not require an account, does not include analytics, and does not collect personal data. The only intended internet communication is the optional update flow via `Velopack`, used to check for and download new releases.

## Support

Aquila is not sold as a commercial product by the maintainer. Optional donations may help support future development, but there are no paid features and no advertising in the app.

## Open-source dependencies

Aquila is built with and made possible by several open-source projects:

| Project | Purpose | License |
|---|---|---|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | hardware sensors and monitoring data | MPL-2.0 |
| [WPF-UI](https://github.com/lepoco/wpfui) | Fluent-style WPF controls and navigation | MIT |
| [Velopack](https://github.com/velopack/velopack) | packaging and in-app updates | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM helpers, source generators, commands | MIT |
| [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) | charts and sparklines | MIT |
| [Microsoft.Extensions.Hosting](https://github.com/dotnet/runtime) | dependency injection and app hosting | MIT |

## License

This project is licensed under the terms of the **Mozilla Public License 2.0 (MPL-2.0)**.

You can find the full text in the `LICENSE` file at the root of this repository, or read it online at https://www.mozilla.org/MPL/2.0/.
