# Sistema visual — vocabulário e arquitetura

Adotado a 2026-08-26. v2 (ramps embutidos, separação das Definições); v3 (termo fixado: **Preset**);
v4 (fronteira casca/preset resolvida, secção Runtime reescrita contra o repositório real, migração
acrescentada); v5 (limiares por `(hardware, métrica)` passaram a código; #42 fechado por desenho);
**v6 a 2026-08-27: a página definida, o Backdrop no lugar do card, e o #17 fechado por consequência.**

---

## Vocabulário

| Termo | O que é | Onde vive |
|---|---|---|
| **Tema** | A casca da app (Dark/Light, WPF-UI + tokens de casca). Cada tema **traz um preset**. | Definições (`Themes/`, `settings.Current.Theme`, `ThemeStyle`) |
| **Preset** | O ficheiro visual completo de *um* widget: mapa de ramps embutidos + secções de aparência por parte do widget. A única coisa que o utilizador cria, guarda e partilha no dia-a-dia. | `presets/*.json`, editor próprio — **fora** das Definições |
| **Ramp** | Escala de cor de 4 estados: `normal`, `elevated`, `alert`, `critical`. Puro dado. | Dentro do preset, num mapa nomeado (`primary` obrigatório) |
| **Página** | Uma tela de tamanho fixo com uma lista de widgets. Escala para caber na janela que a mostra. | `pages/*.json` (futuro); hospedada na `DashboardWindow` |
| **Backdrop** | Um tipo de widget que existe para estar atrás: cor ou imagem, cantos, borda, opacidade. Não contém nada. | Entrada do `WidgetCatalog`, como o gauge ou o chart |
| **Pack** | Instalador para marketplace: preset(s) + tema recomendado + wallpaper (+ ícones) com manifest. Formato de distribuição, não entidade de runtime. | Futuro |

Nomes descartados e porquê: *profile* (sobrecarregado — e **já ocupado** neste código: `ColorProfileService`,
`ColorProfileId`, `Profiles/*.json`); *skin* (dois significados históricos opostos); *theme* para o preset
(colide com a casca); *scheme* (**ocupado** pelas chaves `Aquila.Scheme.*`); *style* (o XAML está cheio de
`<Style>`); *look*, *outfit*, *livery* (preteridos).

Renomear é barato enquanto o formato não for partilhado publicamente. A partir do marketplace é breaking change.

---

## A regra da fronteira

> **Vive só dentro da aplicação? → casca (tema).**
> **Pode viver fora, noutra janela? → preset.**

O motivo não é arrumação. **O que pode sair da app é exactamente o que tem de sobreviver sem a janela à
volta.** Um widget no desktop não tem casca de onde herdar contraste — está sobre um wallpaper que não
escolhemos — e por isso tem de trazer o próprio fundo, borda e opacidade. Um número na barra de título tem
a janela inteira a dar-lhe contexto, e vestir-se de outra coisa faria dele um corpo estranho.

"Pode sair" é um proxy para "tem de ser auto-descritivo".

| Superfície | Sai da app? | Veste-se com |
|---|---|---|
| Widgets no desktop | sim | preset |
| Dashboard (replicável no 2.º ecrã) | sim | preset |
| Molduras dos cards do dashboard | sim, com ela | preset (secções `background`/`border`) |
| Pílulas e fita de pressão da barra de título | não | casca |
| Páginas dentro da app (Storage, Explorer, Definições) | não | casca |

**Cada casca traz um preset.** É isto que impede a regra de ser contraditória: a temperatura do CPU veste-se
de preset num widget e de casca na barra de título, mas as cores de estado vêm de um ramp nos dois casos —
o do preset activo num, o do preset do tema no outro. Não há um segundo conjunto de cores de estado
inventado ao lado.

**Um preset base, não apagável nem editável**, guarda o âmbar que é a identidade da app. É o preset do tema
Aquila e o fundo de todos os outros: duplicar é o caminho para variar.

---

## Princípio estrutural: referência, não posse

Se X pode mudar sem tocar em Y, então Y não é dono de X. A app activa um tema (Definições) e um preset
default (editor), independentes. Um widget referencia um preset por id, num campo **opcional com herança**:

```
preset do widget  →  preset da página (futuro)  →  preset default global
```

**Não há sobreposições por widget.** Ou usas o preset, ou duplicas. Isso elimina o problema da cascata —
"8px por herança" e "8px por escolha" seriam indistinguíveis no ficheiro — ao preço de divergência entre
cópias, que é um custo visível em vez de um bug escondido.

Uma página é um contentor de layout e não tem sistema de cor próprio — o que se vê nela são widgets, e cada
um traz o seu.

**Decisão v2 mantida:** a biblioteca global de ramps fica cortada. O benefício (editar-um-atualiza-todos) é
de marketplace maduro; o custo (ids, colisões no import, referências penduradas, apagar um ramp em uso) é
imediato. Os ramps ficam locais ao preset mas mantêm a **forma** — objecto nomeado com 4 estados — para que
promover um a global seja cópia, sem migração. Desnormalizar agora, normalizar se for preciso.

---

## A página

**Uma página é a lista de widgets do desktop com outro sistema de coordenadas.** No desktop, um widget diz em
que ecrã está e onde; numa página, diz em que página está e onde. Tudo o resto — o construtor, o editor, os
presets, o `MetricKey` — funciona sem uma linha de alteração. Só muda o contentor.

E o contentor já está provado: os widgets do desktop **não** são uma janela cada. Há uma janela por ecrã
(`ScreenCanvasWindow`) com um `Canvas` e os widgets como filhos. Uma página é o mesmo, dentro de uma janela
normal em vez de uma bottom-most — e é por isso que trinta widgets numa página custam menos do que trinta
janelas. (O gargalo real, porém, não são as janelas: são os gráficos LiveCharts por tick. Ver #21/#14.)

**Tamanho fixo, escalado para caber.** Uma janela redimensionável parte coordenadas absolutas, e a
alternativa — ancoragem relativa — é muito mais trabalho para um resultado menos previsível. A página
comporta-se como um slide: tem o seu tamanho, e a janela mostra-a escalada.

Isso resolve de graça uma pergunta que parecia de arquitectura: **redimensionável ou fullscreen?** Nenhuma —
o mesmo código serve as duas, portanto o modo da janela é preferência do utilizador. A `DashboardWindow` já é
redimensionável e guarda posição e tamanho; é a base.

**Um campo, não dois.** O widget diz onde vive num único campo com prefixo — `screen:DEL-A1B2-DP1` ou
`page:overview` — em vez de um `ScreenKey` e um `PageId` com um sempre vazio. O código que resolve a
superfície ramifica pelo prefixo, o que é explícito, e abre a porta a `window:` sem inventar outro campo.

**Não há cards.** Um "card" seria um contentor, com coordenadas relativas, reflow e aninhamento. O que ele
faz de útil — desenhar uma moldura por trás de um grupo — é um **Backdrop** numa camada inferior. Agrupar
(mexer na moldura e os widgets irem atrás) pode chegar depois como um atributo, sem tocar no modelo de
coordenadas.

### Backdrop

Um tipo de widget como qualquer outro: posição, tamanho, camada, preset. O que o distingue é não ler nenhum
sensor — existe para estar atrás.

- Sem imagem, é um rectângulo: cor, opacidade, cantos, borda. O aspecto de card.
- Com imagem, é decoração. O `stretch` decide se enche (`Fill`), corta preservando proporção
  (`UniformToFill`) ou cabe inteiro (`Uniform`, para um logótipo).
- À largura da página e na camada de baixo, é o fundo da página inteira.

Um tipo, uma propriedade a mais, três necessidades — **e fecha o #17**, cuja Fase 1 era exactamente "fundo do
dashboard". Não é preciso sistema nenhum de imagem de fundo: é um Backdrop na camada zero.

O nome descreve a função e não a forma, que é o que o torna legível numa lista ao lado de "Radial gauge" e
"Sparkline". Descartados: *card* (promete contenção), *panel*/*frame*/*canvas*/*border*/*window* (tipos WPF),
*layer* (é já o nosso campo de z-index), *background* (é já a secção do preset), *container* (contém),
*object* (nomeia a categoria, não o membro), *callout* (significa o oposto — algo que aponta e chama a
atenção), *ground* (numa app de hardware, é massa).

**A imagem pertence ao widget, nunca ao preset.** Aparência partilha-se, conteúdo não — e uma imagem é
conteúdo, como um sensor é. O caminho é guardado **relativo à pasta de assets**
(`Documents\Aquilassets\`, ou onde o utilizador definir), nunca absoluto: `assets/carbon-bg.png` sobrevive
à partilha e a mudar de máquina, `C:/Users/joao/...` não desenha em mais lado nenhum. Um Pack escreve os seus
ficheiros nessa pasta ao instalar, e os caminhos passam a resolver sozinhos.

É a mesma regra de sempre: guarda-se a referência, resolve-se no fim.

---

## Contrato de conteúdo

O **widget JSON** contém conteúdo e colocação: sensor(es), tipo de piece, título, **ecrã, posição e
tamanho**, e a referência (`"preset": "carbon"`, opcional). Nunca uma cor.

O **preset** contém todo o visual: o mapa `ramps` + secções por parte do widget. Nunca sensores, posições
ou limiares.

**Regra das cores:** um elemento que representa **estado de dados nunca tem cor directa** — referencia um
ramp por nome (`"ramp": "primary"`). Cor fixa faz-se com um **ramp plano** (4 stops iguais); o editor
oferece um toggle "cor fixa" que escreve exactamente isso. Um mecanismo, sem bypass.

Isto dissolve duas coisas de uma vez. A dualidade fixo/segue deixa de existir — não há dois modos, há um
parâmetro. E **fecha o #42**: no formato antigo, os papéis apontavam para *degraus numerados* de uma paleta
partilhada, os degraus 4 e 5 pertenciam ao `alert` e ao `critical`, e escrever `"series3": 5` desenhava uma
série perfeitamente saudável com a cor do alarme. Nada validava isso, e a proposta era um aviso no carregamento.
Com ramps não há degraus partilhados para apontar por engano: a cor de alarme de uma série é, por construção,
a cor que ela tem *quando está em alarme*. O modo de falha que sobra — escrever um ramp cujo `normal` é o
vermelho da casa — exige querer, em vez de bastar trocar um algarismo.

Elementos de moldura (fundos, bordas, títulos) têm cor directa + opacidade separada.

**Ramp primário como fallback:** um chart com mais linhas do que ramps desenha, usando o `primary` nas
restantes, em vez de rebentar ou inventar.

**Teste decisivo:** o mesmo preset tem de vestir um widget de CPU e um de rede sem editar nada. Se não
conseguir, tem lá dentro algo que pertence ao widget.

---

## Limiares — fora do preset, chaveados por `(hardware, métrica)`

Trocar de visual nunca muda quando algo é considerado crítico. Os limiares são **configuração global de
monitorização**, vivem nas Definições e são partilhados por tudo o que julga leituras.

**Construído.** A chave é `MetricKey(HardwareKind, MetricKind)`, escrita `"Cpu.Temperature"` onde tem de ser
texto: definições, `widgets.json` e o `ConverterParameter` do XAML. Substituiu uma família única que não
conseguia dizer que um GPU aos 83 °C é normal e um CPU aos 83 °C é elevado.

**A taxonomia já existia na árvore tipada**, como aninhamento — a propriedade exterior é a peça, o grupo
interior é a métrica:

| Hardware | Métricas |
|---|---|
| `Cpus[i]` | Load, Temperature, Power, Clock |
| `Memory` | Load, Data, Virtual, Dimms |
| `Gpus[i]` | Load, Temperature, Clock, Data, Power, Fan |
| `Motherboard` | Temperature, Voltage, Fan, Control |
| `Networks[i]` | Throughput, Data |
| `Storages[i]` | Load, Data, Temperature, Level, Factor, Throughput |

**Mas morria na folha.** O `SensorNode` tem `Value`, `Min`, `Max`, `Unit`, `Name`, `Identifier`, `History` —
e mais nada. Com um `SensorNode` na mão, 62 °C num NVMe e 62 °C num die são o mesmo objecto.

**Carimbada no `SensorCatalog`**, que é o único sítio com as duas metades: o ciclo sabe que está a percorrer
CPUs, e cada `yield` do construtor sabe que `c.Temperature.Primary` é uma temperatura. Nenhum sabe sozinho.

**`Thresholds.Preset(key)` devolve null** quando o par não tem escala partilhada — watts, relógios, volts,
bytes por segundo. Essa tabela **é** a definição de "julgável": o formulário deriva dela as suas linhas, por
isso não pode haver uma segunda lista a discordar. **Onze linhas**, todas correspondentes a leituras que a
máquina pode realmente produzir.

**Defaults por dispositivo, já disponíveis.** O modelo *já traduz* os limites que o hardware reporta e
nunca os leu:

```
StorageTemperatureNode  →  Primary, Warning, Critical
DimmNode                →  Temperature, WarningTemperature, CriticalTemperature,
                           LowTemperature, CriticalLowTemperature
```

Precedência: **valor do utilizador → limite do dispositivo → preset embutido.** Construído: com dois
dispositivos ganha o mais frágil, e a linha das definições diz "Reported by the hardware" enquanto for esse
o caso.

O dispositivo dá **dois** dos três números. O primeiro degrau mantém a *proporção* que o nosso preset lhe
dá em relação ao aviso, ancorada no número do dispositivo — onde começa a aquecer é um juízo nosso, onde
está o aviso é um facto dele. Fixá-lo a um grau abaixo do aviso foi tentado e produzia rampas degeneradas:
um DIMM a reportar 55 e 85 dava 54/55/85, um grau de banda seguido de trinta.

**`Pressure` não é configurável.** Colore um valor que já passou pela normalização, e os seus degraus *são*
essa normalização.

---

## Resolução de um piece

```
preset do widget (?? página, futuro) ?? default global
  → secção da parte do widget
    → ramp local por nome (em falta → primary)
      → cor do estado actual
```

O estado vem do `VitalMonitor`: `RoleFor(valor, hardware, métrica)` → `Normal | Elevated | Alert | Critical`.

Três saltos, sempre os mesmos.

---

## Esboço dos ficheiros

```jsonc
// widget — conteúdo e colocação
{ "id": "cpu-1", "kind": "gauge", "title": "CPU",
  "series": [ { "sensor": "/amdcpu/0/load/0", "ramp": "primary" } ],
  "surface": "screen:DEL-A1B2-DP1", "pos": [32, 150], "size": [170, 190], "layer": 0,
  "preset": "carbon" }

// backdrop — o mesmo, sem sensores. A imagem é conteúdo, logo vive aqui e não no preset.
{ "id": "bg-1", "kind": "backdrop",
  "surface": "page:overview", "pos": [0, 0], "size": [1920, 1080], "layer": -1,
  "image": "assets/carbon-bg.png", "stretch": "uniformToFill",
  "preset": "carbon" }

// preset — todo o visual, autocontido
{ "id": "carbon",
  "ramps": {
    "primary":   { "normal": "#F5A623", "elevated": "#FF8C42", "alert": "#FF6B35", "critical": "#FF4444" },
    "secondary": { "normal": "#60CDFF", "elevated": "#4FB3E8", "alert": "#FF6B35", "critical": "#FF4444" } },
  "background": { "color": "#101014", "opacity": 0.85,
                  "border": { "thickness": 1, "color": "#FFFFFF", "opacity": 0.08 } },
  "gauge":      { "ramp": "primary", "thickness": 14, "corner": 0, "sweep": "dial",
                  "track": { "color": "#FFFFFF", "opacity": 0.08 } },
  "line":       { "ramp": "primary", "thickness": 1.5, "fill": "gradient",
                  "fillOpacity": 0.30, "smoothness": 0.5, "pointSize": 0 },
  "bar":        { "ramp": "primary", "thickness": 6, "corner": 3 },
  "title":      { "fontFamily": "Segoe UI", "size": 12, "opacity": 0.6 },
  "value":      { "fontFamily": "Segoe UI", "size": 24 } }
```

**A superfície é obrigatória no widget.** O `screen:` mantém a história inteira que já existe por trás dele
— identidade estável por EDID, com fallback para o ecrã primário quando o monitor desaparece. O `page:` não
precisa de nenhuma dessas defesas: uma página não é desligada da tomada.

**As secções do preset já estão meio decididas pelo código.** O editor actual tem exactamente estas por
tipo: `Line` (sparkline/chart), `Dial` (gauge), `Bar` (meter), `Number` (stat), mais `Appearance`
(fundo/borda) e `Layout`. Fixar `gauge`/`line`/`bar`/`number`/`background`/`title`/`value` alinha o JSON
partilhado, o editor e o código de uma vez.

**Convenções:** chaves em inglês e **aninhadas** — mapeiam directamente para records C# (System.Text.Json) e
para as secções do editor. Opacidade separada da cor (color picker + slider; compõem-se no resolve). Fontes
por família, com fallback para a default quando não estiver instalada — e o editor deve **avisar** quando um
preset importado nomeia uma fonte que a máquina não tem, senão degrada em silêncio. Para o `value`, preferir
dígitos tabulares: números que mudam a cada tick não devem dançar em largura.

---

## Runtime — o que existe hoje

*(Secção reescrita na v4. A v3 citava `ThermalBrushConverter`, `AccentBrushProvider` e `Aquila.Ram`, que
não existem — o documento tinha sido escrito contra um estado anterior do código.)*

| Peça | Estado |
|---|---|
| `ColorProfileService` | carrega `Profiles/*.json` (4 embutidos: classic, ember, signal, sky) e publica `Aquila.Scheme.{Role}` em `Application.Resources` |
| Papéis publicados | `Accent`, `Normal`, `Elevated`, `Alert`, `Critical`, `Series1/2/3`, `Track` — **é o proto-ramp** |
| `VitalMonitor` | limites em vigor + `RoleFor`/`BrushFor`; `Current` estático para o XAML alcançar |
| `IntensityBrushConverter` | adaptador fino: recebe valor + família e delega |
| `Thresholds` | famílias, presets embutidos, `Level` (0–1 em degraus) e `Role` |
| `SensorCatalog` | carimba `SensorEntry.Family` — o sítio onde `Hardware` + `Metric` entram |
| `DesktopWidgetService` | `SetResourceReference` para papel fixo, ou `FollowReading` por tick |

`Aquila.Scheme.*` está em **23 ficheiros**. O caminho é: `ColorProfileService` → `PresetService` (gémeo do
`SettingsService`), com o mesmo padrão de publicar brushes estáveis em `Application.Resources` e `SKColor`
para o `AquilaCharts`. O XAML continua em `DynamicResource`; os conversores passam a resolver
estado → stop do ramp em vez de estado → papel global.

Mudança de tema continua no `AppearanceService` (`SystemEvents.UserPreferenceChanged` + leitura directa de
`AppsUseLightTheme`; **não** `SystemThemeWatcher.Watch`, que aplica Fluent puro e deita fora o overlay).

---

## Migração

É a parte que mais provavelmente corre mal, e não estava nas versões anteriores.

O `widgets.json` de hoje guarda **mais de vinte campos de aparência por widget**: `BackgroundColor`,
`BackgroundOpacity`, `CornerRadius`, `BorderColor`, `BorderOpacity`, `BorderThickness`, `LineThickness`,
`Fill`, `LineSmoothness`, `PointSize`, `ArcThickness`, `ArcCorner`, `Sweep`, `ValueSize`, `ShowValue`,
`BarThickness`, `BarCorner`, `BarValueSize`, `Layout`, `StatValueSize`, `ShowUnit`, `UnitSize`, `ShowPanel`.

Três saídas, por ordem de preferência:

1. **Um preset gerado por layout, não por widget.** Ao migrar, escrever um único preset "Importado" a partir
   dos valores do primeiro widget e apontar todos lá. Perde variação entre widgets, mas ninguém perde o
   aspecto geral e o resultado é editável a partir daí.
2. **Um preset por widget distinto** (agrupando os que já são iguais). Fiel, mas pode gerar doze presets que
   o utilizador nunca pediu.
3. **Descartar e apontar todos ao base.** Só aceitável enquanto o número de utilizadores for ~1.

Seja qual for, é uma migração guardada por `SettingsVersion` — **nunca chaveada no estado que repara**, ou
não distingue "nunca migrado" de "o utilizador mudou de ideias" e desfaz-se a cada arranque.

---

## Editores e Definições

**Um único editor de preset**, com secções iguais às do JSON. A secção `ramps` são linhas de 4 color pickers
com importar/exportar (fragmento de 4 cores) e o toggle "cor fixa".

**O editor de widgets** fica com o *picker* de preset + "editar" + "duplicar"; "definir como default" vive
aqui. Deixa de ter colunas de cor por série — a série escolhe um **ramp por nome**, não uma cor.

**As Definições da app** ficam com comportamento, **Tema** e **Limiares**. Nada de presets.

Editar um preset partilhado afecta todos os widgets que o vestem; "duplicar e editar" é o caminho para
variação sem surpresas. O preset base não é editável nem apagável.

---

## Partilha

O preset é autocontido — os ramps vão dentro, nunca chega "despido". Ramps partilham-se como fragmentos via
importar/exportar.

O **Pack** é um zip com manifest tipado (presets, tema recomendado, wallpaper, ícones um dia). Instalar
distribui os conteúdos pelos sistemas respectivos; desinstalar usa o manifest. **Em runtime não existe "pack
activo"** — existe preset activo, tema activo, wallpaper definido.

Um pack que traz um preset **escreve uma definição no momento da instalação**; não conduz o preset em tempo
de execução. Se o utilizador trocar depois para o seu, fica — reinstalar o tema não lho arranca. É a mesma
regra de sempre: guarda-se o que foi *escolhido*, não o que está em vigor.

Uma marca pode chamar "XYZ Theme" ao seu pack a nível de marketing sem tocar no modelo interno.

---

## Em aberto (design, não arquitectura)

- **Ramps monocromáticos** (a linha mantém a identidade até `critical`) **vs convergentes** (todos acabam no
  vermelho da casa — reconhecimento instantâneo, mas duas linhas críticas no mesmo chart ficam ambíguas).
  São apenas ramps diferentes; a escolha é do preset.
- **Variantes Dark/Light por ramp** — só se a legibilidade no tema claro vier a pedi-las.
- **Vocabulário das partes do widget** — fixar `title`/`value`/`legenda` **antes** do primeiro formato
  partilhado. Aparece no JSON, no editor e no código.
- **Ordem de execução:** ~~`SensorEntry` com `Hardware` + `Metric`~~ (feito) → ~~hierarquia~~ (decidida:
  página = tela fixa + lista de widgets; sem cards) → **`Surface` num campo só** → **Backdrop no catálogo** →
  `PresetService` e formato → migração → packs.

  Os dois primeiros passos por fazer são pequenos e independentes do formato de preset: o campo `surface`
  substitui o `ScreenKey` sem mudar comportamento, e o Backdrop é uma entrada no `WidgetCatalog` que não lê
  sensores. Ambos podem começar já.
