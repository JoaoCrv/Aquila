# Contexto da Aplicação Aquila (Monitor de Hardware)

## Resumo da Aplicação:

Sistema em tempo real para monitoramento de hardware, construído com WPF (.NET 9) e padrão MVVM. Exibe dados como CPU, GPU, memória e armazenamento via `LibreHardwareMonitorLib`, com atualizações a cada 1 segundo usando `DispatcherTimer`.

- **WPF**: Interface gráfica com GPU acceleration (DirectX) e suporte ao Fluent Design via pacote `WPF-UI v4.0.2`.
  - Async using statements enhancements
- **MVVM**: Utiliza `CommunityToolkit.Mvvm` para separação clara entre lógica de negócio e interface.
- **Atualizações Automáticas**: Mecanismo de atualização integrado com `Velopack`.

## Arquitetura:

```
┌───────────────┐
│  Services     │ ← Lê dados via LibreHardwareMonitorLib
├───────────────┤
      ↑ ↓
┌───────────────┐       ┌───────────────┐
│ ViewModel     │◄───┬──│   View        │ (WPF com Data Binding)
│ (MVVM)        │    │  │ (Fluent UI)   │
└───────────────┘    ▼  └───────────────┘
┌───────────────┐       ┌───────────────┐
│ DispatcherTimer│ → Atualiza dados a cada 1000ms │
└───────────────┘
```

## Pontos Chave:

- **Data Binding automático**: Alterações nos serviços são refletidas na interface sem código manual.
- **Modularização**: Separado em Services/ViewModels para facilidade de manutenção.
- **Design Consistente**: Temas Fluent Design da Microsoft via `WPF-UI`.

## Dependências Críticas:

1. **LibreHardwareMonitorLib**: Fonte de dados de hardware
2. **CommunityToolkit.Mvvm**: Implementação MVVM oficial
3. **WPF-UI**: Componentes visuais com suporte a Fluent Themes

## Fluxo de Dados:

Sensores físicos → `LibreHardwareMonitorLib` → Services (processamento) → ViewModel → View (via Binding)

```

```
