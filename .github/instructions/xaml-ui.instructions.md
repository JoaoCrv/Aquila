---
applyTo: "Views/**/*.xaml,Controls/**/*.xaml,Styles/**/*.xaml,Themes/**/*.xaml,Views/**/*.xaml.cs,Controls/**/*.xaml.cs"
description: "Use when editing Aquila's WPF UI, XAML pages, user controls, styles, or theme resources. Reuse existing cards, styles, converters, and DynamicResource theme tokens."
---

# Aquila XAML UI Instructions

## Follow the existing UI structure

- Match the patterns already used in `Views/Pages/DashboardPage.xaml`, `Views/Windows/MainWindow.xaml`, `Styles/SensorStyles.xaml`, and `Themes/Aquila.Default.xaml`.
- Prefer the repo's existing composition style: `ui:Card`, `StackPanel`, `WrapPanel`, `UniformGrid`, and small stat-box sections instead of introducing a new layout language.
- Keep pages and controls visually consistent with the current spacing, corner radius, and compact dashboard-style information density.

## Binding and page composition

- Pages typically expose their injected ViewModel as `ViewModel` and bind through `DataContext = this`; in page XAML, prefer bindings like `ViewModel.PropertyName`.
- Keep code-behind minimal and UI-only. Put state, formatting decisions, and commands in the relevant `*ViewModel` unless it is purely visual wiring.
- Reuse existing converters from `Helpers/` before adding new code-behind logic.

## Styling and theming

- Prefer shared styles such as `SectionLabel`, `ChipName`, `RowLabel`, `RowValue`, `StatBox`, and the named progress-bar styles in `Styles/SensorStyles.xaml`.
- Use `{DynamicResource Aquila.*}` and existing WPF-UI theme brushes instead of hardcoded colors or one-off brushes in page markup.
- If a new reusable color/token is needed, add it in `Themes/Aquila.Default.xaml` and keep it compatible with the `AccentBrushProvider` runtime pattern.

## Responsive and conditional UI

- Reuse `ColumnWidthConverter`, `ResponsivePaddingConverter`, `NullToCollapsedConverter`, and `ZeroToCollapsedConverter` for responsive or optional sections.
- Avoid fixed widths and rigid layouts unless there is a clear reason; prefer the existing responsive behavior used in the dashboard.
- For lightweight history charts, reuse `controls:SparklineChart` instead of embedding a new chart setup inline.

## Keep changes small and consistent

- Reuse `Controls/`, `Styles/`, and `Themes/` assets before introducing new abstractions.
- When a XAML change requires new data, extend the corresponding `*ViewModel` or helper cleanly rather than hardcoding UI-only values.
- Preserve null-safe display behavior (`FallbackValue`, collapsed empty sections, placeholder text like `--`) because hardware data may be missing on some machines.
