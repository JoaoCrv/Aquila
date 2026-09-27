# Grounds

Not a theme and not loaded by anything. A record of the surface levels Aquila has worn, so that trying one
again is pasting six lines over the palette half of `Themes/Aquila/Dark.xaml` rather than reconstructing it
from history — where it only exists inside the file's *previous structure*, before the palette and the
assignments were separated.

Only these six differ. The rest of the palette — the control face, its hover and pressed, the lines, the
text, the accent — is the same in both, which is what makes the ground a genuinely independent decision.

## Warm (the original)

Neutrals carrying a trace of the logo's warmth. Read as a deliberate surface rather than a tint, and let
the data colours sit on something related to them.

```xml
<Color x:Key="Aquila.Surface.Below">#12100D</Color>
<Color x:Key="Aquila.Surface.App">#15110E</Color>
<Color x:Key="Aquila.Surface.Well">#1A1512</Color>
<Color x:Key="Aquila.Surface.Card">#1F1A16</Color>
<Color x:Key="Aquila.Surface.Scrim">#B315110E</Color>
<Color x:Key="Aquila.Line.Quiet">#2B2420</Color>
```

## Cool (from the design canvas, 2026-09-27)

Cool neutrals, barely off grey. Lighter than the warm ground by about eleven steps at the card. The
argument for it: the presets paint in ambers, so with a neutral ground every orange on screen is
*meaning* — the accent, a switch that is on, a reading that is high. On the warm ground the application
competed quietly with the data it was drawing.

```xml
<Color x:Key="Aquila.Surface.Below">#1A1A1E</Color>
<Color x:Key="Aquila.Surface.App">#202024</Color>
<Color x:Key="Aquila.Surface.Well">#252529</Color>
<Color x:Key="Aquila.Surface.Card">#2A2A2F</Color>
<Color x:Key="Aquila.Surface.Scrim">#B3202024</Color>
<Color x:Key="Aquila.Line.Quiet">#2B2420</Color>   <!-- still warm; unresolved against this ground -->
```

## Before pasting either one

**The scrim's last six digits must equal `App`.** It is the window's own colour at seventy percent over the
Mica, written out because XAML cannot add an alpha to another colour.

**Re-check the states against the new ground, by the transition and not by the pair.** A state is only
ever seen as a change from another state. `Aquila.Surface.Pressed` (`#2B2B31`) sits 0.85 dE from this card
and looks alarming written down, but it is reached only from hover (7.92 dE away) or from rest (3.79),
so the press reads perfectly well.

The one to actually look at is `Aquila.Surface.Disabled` (`#26262B`), 1.90 dE from the card: a resting
state, with no transition to carry it, legible only because the text greys with it. It also went from
sitting above this card to sitting below it, turning a disabled control from raised to sunken.

And before reaching for a new number: app to card is 4.88 dE, card to rest 4.43. There is no room to
insert a level between them — anything placed inside lands about 2.2 from each neighbour.
