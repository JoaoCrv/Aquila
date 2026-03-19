# Aquila Project Context

## 📋 Resumo do Projeto

**Aquila** é uma aplicação desktop de monitorização de hardware construída em .NET 9 com WPF. O objetivo principal da aplicação é fornecer um painel simples e elegante para visualizar métricas-chave do sistema (CPU, RAM, temperatura, etc.), sendo ideal para utilização em ecrãs secundários ou como dashboard de sistema.

A aplicação segue o padrão **MVVM** rigorosamente, utiliza **Dependency Injection** via .NET Generic Host, e emprega a biblioteca **WPF-UI** para uma interface moderna com design Fluent (estilo Windows 11). Inclui também funcionalidade de atualização automática via **Velopack**.

---

## 🏗️ Arquitetura do Projeto

### Padrão: MVVM (Model-View-ViewModel)
A separação de responsabilidades é estrita:
- **Models**: POCOs que representam a estrutura de dados (ex: `AppConfig`, `HardwareMonitorModel`).
- **Views**: Interfaces WPF (.xaml) que definem a UI. Não contêm lógica de negócio, apenas binding e eventos.
- **ViewModels**: Lógica da aplicação, exposição de dados para a View (`ObservableProperty`), comandos (`RelayCommand`) e injeção de dependências.

### Injeção de Dependência (DI) via .NET Generic Host
A aplicação utiliza o modelo **Generic Host** do .NET para gerir o ciclo de vida:
- Configuração centralizada em `App.xaml.cs`.
- Registo de serviços Singleton (ex: `HardwareMonitorService`, `UiService`) e Transientes.
- Container de DI gerido via `Microsoft.Extensions.Hosting`.

### Estrutura de Navegação
A aplicação é baseada em navegação interna, utilizando o componente `ui:NavigationView` do WPF-UI para alternar entre páginas (Dashboard, Explorer, Storage, About, Settings) sem criar múltiplas janelas.

---

## 🛠️ Stack Tecnológico e Dependências

### Runtime & Framework
- **.NET 9** (`net9.0-windows`) - A versão mais recente do .NET para aplicações desktop Windows.
- **C# 13** - Linguagem com features modernas (collection expressions, raw string literals, etc.).
- **WPF** - Windows Presentation Foundation para UI Desktop.

### Bibliotecas Principais (`Aquila.csproj`)

| Dependência | Versão | Descrição / Função |
|-------------|--------|-------------------|
| `LibreHardwareMonitorLib` | 0.9.4 | Biblioteca core para leitura de sensores (temperatura, voltagens, frequências, uso de CPU/RAM). |
| `Velopack` | 0.0.1298 | Framework de atualização automática (auto-updater) e empacotamento da aplicação. |
| `WPF-UI` | 4.0.2 | Biblioteca de controles UI com design Fluent (estilo Windows 11). Inclui componentes como NavigationView, Cards, AppBars. |
| `WPF-UI.DependencyInjection` | 4.0.2 | Extensões para integrar WPF-UI com o container de DI do .NET. |
| `Microsoft.Extensions.Hosting` | 9.0.1 | Framework para criar serviços hospedados, configuração e logging (Generic Host). |
| `CommunityToolkit.Mvvm` | 8.4.0 | Toolkit oficial da Microsoft para implementação rápida e segura de MVVM (ObservableObject, RelayCommand). |

### Configurações do Projeto (.NET 9)
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`) - Segurança de referências ativa.
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`) - Redução de verbosity em namespaces globais (ex: `System`, `System.Linq`).

---

## 🧩 Componentes Principais

### Services
1.  **`HardwareMonitorService`**: Gerencia a conexão com o `LibreHardwareMonitor`, executa leituras de sensores e notifica atualizações quando os dados mudam.
2.  **`UiService`**: Gerencia o estado da interface (ex: controlo do indicador "IsLoading"), manipulação de temas (Claro/Escuro) via `IThemeService`.

### Views & ViewModels Mapeados
- **DashboardPage / DashboardViewModel**: Visualização principal com gráficos e sensores em tempo real.
- **ExplorerPage / ExplorerViewModel**: Navegação hierárquica dos componentes de hardware detectados.
- **StoragePage / StorageViewModel**: Monitorização de discos (espaço livre, uso).
- **AboutPage / AboutViewModel**: Informações sobre a aplicação e créditos.
- **SettingsPage / SettingsViewModel**: Configurações da aplicação (tema, auto-startup, etc.).

---

## ⚠️ Restrições Técnicas & Boas Práticas

1.  **Threading**: A leitura de hardware ocorre em background threads (`Task`, `Timer`). Qualquer atualização de UI que dependa desses dados deve ser feita no thread principal (gerido automaticamente pelo Dispatcher do WPF ou via binding).
2.  **UI Updates**: Evitar chamadas diretas a controlos de UI fora do thread principal. Utilizar o padrão `ObservableProperty` para atualizações reativas.
3.  **Theming**: A aplicação suporta temas Claro/Escuro dinâmicos. Não hard-codificar cores; utilizar `DynamicResource`.
4.  **Atualizações**: O fluxo de atualização via Velopack é crítico. Nunca bloquear o thread principal durante checks de update.

---

## 📂 Estrutura do Workspace
```text
Aquila/
├── .continue/              # Contextos para Continue (IDE extension)
│   └── aquila_project_context.md
├── Aquila.csproj           # Definição do projeto e dependências
├── App.xaml / App.xaml.cs  # Entry point, DI container setup, Velopack init
├── Views/                  # Interfaces WPF (.xaml)
│   ├── Windows/
│   │   └── MainWindow.xaml (Container de navegação)
│   └── Pages/              # Páginas individuais (Dashboard, Settings, etc.)
├── ViewModels/             # Lógica e binding (MVVM)
│   ├── Windows/
│   │   └── MainWindowViewModel.cs
│   └── Pages/              # ViewModels das páginas
├── Services/               # Serviços de negócio
│   ├── HardwareMonitorService.cs
│   └── UiService.cs
├── Models/                 # Estruturas de dados (POCOs)
├── Helpers/                # Utilitários (converters, extensões)
└── Assets/                 # Recursos estáticos (ícones, imagens)
```

---

## 🎯 Instruções para o Assistente AI

Ao gerar código ou responder a perguntas sobre este projeto:
- **Sempre** use `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`) em vez de implementação manual de INotifyPropertyChanged.
- **Não** sugerir bibliotecas externas que não estejam listadas acima sem justificação forte.
- **Respeitar** a arquitetura .NET Generic Host para DI e lifecycle management.
- **Priorizar** componentes do WPF-UI (`ui:Control`) sobre controles padrão do WPF para manter o design Fluent.

---
*Contexto gerado automaticamente para facilitar a interação com o modelo LM Studio via Continue.*
