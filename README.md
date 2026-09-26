# Glücksrad für Streamer.bot & OBS

Ein animiertes Glücksrad für deinen Stream. Es dreht sich bei Kanalpunkten, Bits, Subs, Gift-Subs, Tipps oder per Chat-Befehl.
Alles wird in einem **Einstellungsfenster** eingerichtet, ohne HTML oder JavaScript anzufassen:

- beliebig viele Räder (z. B. „Kanalpunkte“ und „Spenden“)
- Felder mit Text, Farbe, Bild, Chance, Chatnachricht, Sound, Freispin und einer Gewinn-Action
- Zuordnung der Auslöser zu den Rädern (welche Belohnung, ab wie vielen Bits usw.)
- Aussehen (Drehdauer, Umdrehungen, Farben, Einblendung, Tick-Sound)
- **OBS wird automatisch eingerichtet:** Pro Rad entsteht eine Szene `Glücksrad – <Name>` mit einer Browser-Quelle.
  Bei jedem Speichern wird sie aktualisiert, und beim Umbenennen wird auch in OBS umbenannt.

![Einstellungsfenster](docs/einstellungen.png)

## Voraussetzungen

- **Streamer.bot 1.0** oder neuer
- **OBS Studio 28** oder neuer, in Streamer.bot verbunden (*Stream Apps → OBS*)
- Der **WebSocket-Server** von Streamer.bot ist aktiv (*Servers/Clients → WebSocket Server*, „Auto Start“ an, Standard `127.0.0.1:8080`)

## Installation

1. Öffne [`dist/Gluecksrad_Import.txt`](dist/Gluecksrad_Import.txt) und kopiere den kompletten Inhalt.
2. Klicke in Streamer.bot oben auf **Import**, füge den Text ein und bestätige mit **Import**.
   Danach gibt es die Gruppe **Glücksrad** mit drei Actions:
   | Action | Zweck |
   |---|---|
   | **Glücksrad – Einstellungen** | öffnet das Einstellungsfenster |
   | **Glücksrad – Drehen** | hier hängst du alle Auslöser (Trigger) an |
   | **Glücksrad – Ergebnis** | wird vom Rad aufgerufen, wenn es stehen bleibt. Nicht verändern. |
3. Führe **Glücksrad – Einstellungen** aus, z. B. per Rechtsklick auf die Action → *Run Action*.
   Tipp: Leg dir dafür einen Hotkey-Trigger an.
4. Richte im Fenster dein Rad ein und klicke auf **Speichern**. Streamer.bot legt in OBS dann die Szene
   `Glücksrad – <Name>` mit der Browser-Quelle an.
5. Damit das Rad im Stream zu sehen ist, wähle im Reiter **OBS** unter „Zusätzlich einfügen in“ deine Live-Szene.
   Alternativ ziehst du die Szene selbst als Quelle in deine Szenen. Das Rad ist nur während einer Drehung sichtbar.
6. Hänge in Streamer.bot an die Action **Glücksrad – Drehen** die Trigger an, die ein Rad drehen sollen:
   - Kanalpunkte: *Twitch → Channel Reward → Reward Redemption* (für jede Belohnung einen Trigger)
   - Bits: *Twitch → Chat → Cheer*
   - Subs: *Twitch → Subscriptions → Subscription / Resubscription / Gift Subscription / Gift Bomb*
   - Tipps: z. B. *StreamElements → Tip*, *Streamlabs → Donation*, *Ko-fi → Donation*
   - Chat-Befehl: *Core → Commands → Command Triggered* (den Befehl vorher unter *Commands* anlegen)
7. Lege im Reiter **Auslöser** fest, welches Rad bei welchem Ereignis dreht. Beispiel: Die Belohnung
   „Glücksrad“ dreht das Rad *Kanalpunkte*, Bits ab 500 drehen das Rad *Spenden*.
8. Mit **Test-Drehen** probierst du alles aus. Die Chatnachricht bekommt dann den Zusatz `[Test]`,
   und Gewinn-Actions werden bei Tests **nicht** ausgeführt.

## Felder & Gewinne

| Spalte | Bedeutung |
|---|---|
| Text auf dem Rad | Beschriftung des Feldes (darf leer sein, wenn ein Bild den Text enthält) |
| Farbe / Textfarbe | anklicken, um die Farbe zu wählen |
| Bild | anklicken → Bild wählen (PNG, JPG, GIF, WebP, SVG; max. 5 MB) |
| Chance | Gewichtung. Die Summe muss **nicht** 100 sein; der Anteil in % steht daneben. |
| Chatnachricht | wird im Chat gepostet. Platzhalter: `%user%`, `%prize%`, `%wheel%` |
| Gewinn-Action | optionale Streamer.bot-Action, die beim Gewinn ausgeführt wird |
| Sound | optionaler Sound beim Gewinn |
| Freispin | der Gewinner darf sofort noch einmal drehen |

Die **Gewinn-Action** bekommt diese Argumente:
`%user%` (Gewinner), `%gluecksradPrize%` (Feldtext), `%gluecksradWheel%` (Radname), `%gluecksradField%` (Feldnummer) und `%gluecksradTest%`.
Damit lassen sich z. B. Punkte vergeben (`!editpoints %user% 10000`), OBS-Quellen einblenden oder Sounds abspielen.

## Gut zu wissen

- Das Gewinnfeld wird **in Streamer.bot ausgelost**, nicht im Browser. Wird eine Browser-Quelle
  doppelt geladen, zählt das Ergebnis trotzdem nur einmal.
- Mehrere Drehungen hintereinander landen in einer Warteschlange und laufen nacheinander.
- Ein Rad kann auch manuell gedreht werden: Setze vor dem Aufruf von „Glücksrad – Drehen“ das Argument
  `wheel` auf den Namen des Rads (z. B. mit einer *Set Argument*-Subaction).
- Die Overlay-Dateien liegen im Streamer.bot-Ordner unter `gluecksrad\rad_<id>.html`. Sie werden beim Speichern automatisch geschrieben.
- Die Einstellungen stehen in der persistenten globalen Variable `gluecksrad_config`.

## Für Entwickler

| Pfad | Inhalt |
|---|---|
| `overlay/overlay.html` | Overlay (Canvas, Animation, WebSocket). Mit `?demo=1` kann man es ohne Streamer.bot testen (Klick = Drehung). |
| `streamerbot/*.cs` | Quellcode der Actions. `Shared.cs` wird in jede Action eingefügt. |
| `tools/build_import.py` | baut `dist/Gluecksrad_Import.txt` und `dist/*.cs` |

Nach Änderungen: `python tools/build_import.py` ausführen und `dist/` mit committen.
