# Glücksrad für Streamer.bot

Ein interaktives Glücksrad für Streamer.bot mit umfangreichen Anpassungsmöglichkeiten.

## Installation

1. Laden Sie alle Dateien in einen Ordner Ihrer Wahl herunter
2. Öffnen Sie die `settings.html` in einem Browser, um das Glücksrad anzupassen
3. Importieren Sie die `StreamerBot_Import` Datei in Streamer.bot

## Streamer.bot Import

1. Öffnen Sie Streamer.bot
2. Klicken Sie auf "Import" in der oberen Menüleiste
3. Wählen Sie die `StreamerBot_Import` Datei aus
4. Bestätigen Sie den Import

Nach dem Import wird eine neue Action erstellt, die das Glücksrad startet. Sie können diese Action mit einem Chat-Befehl oder einem anderen Trigger verknüpfen.

## Einstellungsmöglichkeiten

### Allgemeine Einstellungen

- **Dauer**: Die Dauer der Animation in Sekunden (Standard: 35)
- **Spins**: Anzahl der Umdrehungen (Standard: 25)
- **Äußerer Radius**: Größe des Rades (Standard: 310)
- **Innerer Radius**: Größe des Mittelpunkts (Standard: 80)
- **Linienbreite**: Breite der Segmenttrennlinien (Standard: 9)

### Segment-Einstellungen

Jedes Segment kann individuell angepasst werden:

- **Bild**: Hintergrundbild für das Segment
- **Farbe**: Hintergrundfarbe des Segments
- **Text**: Text, der im Segment angezeigt wird
- **Schriftgröße**: Größe des Textes
- **Textfarbe**: Farbe des Textes
- **Gewinn**: Name des Gewinns
- **Nachricht**: Nachricht, die bei Gewinn angezeigt wird
- **Wahrscheinlichkeit**: Relative Wahrscheinlichkeit für dieses Segment (1-100)

### Animation-Einstellungen

- **Blink-Dauer**: Wie lange das Gewinnersegment blinkt (in ms)
- **Blink-Intervall**: Zeit zwischen den Blinks (in ms)
- **Blink-Farbe**: Farbe des Blinkens

### Modus-Einstellungen

- **Modus 1**: Segmentgrößen basieren auf Wahrscheinlichkeiten
- **Modus 2**: Alle Segmente haben die gleiche Größe

## Anpassung der Segmente

1. Öffnen Sie `settings.html`
2. Wählen Sie ein Segment aus der Liste
3. Passen Sie die Einstellungen an
4. Klicken Sie auf "Speichern"
5. Klicken Sie auf "Index generieren" um die Änderungen zu übernehmen

## Bilder hinzufügen

1. Legen Sie Ihre Bilder im `image` Ordner ab
2. Verwenden Sie die Bilder in den Segment-Einstellungen
3. Unterstützte Formate: PNG, JPG, GIF

## Fehlerbehebung

- Wenn das Rad sich nicht dreht, überprüfen Sie die Browser-Konsole (F12) auf Fehlermeldungen
- Stellen Sie sicher, dass alle Bilder korrekt geladen werden
- Überprüfen Sie die Streamer.bot-Verbindung (WebSocket muss aktiv sein)

## Bekannte Einschränkungen

- Maximale Anzahl von Segmenten: 12
- Maximale Bildgröße: 500x500 Pixel
- Unterstützte Browser: Chrome, Firefox, Edge (neueste Versionen)

## Support

Bei Fragen oder Problemen:
1. Überprüfen Sie die Fehlerbehebung
2. Stellen Sie sicher, dass alle Abhängigkeiten installiert sind
3. Kontaktieren Sie den Support über GitHub Issues

## Features

- **Vollständig anpassbares Glücksrad** (Segmentanzahl, Farben, Wahrscheinlichkeiten, Bilder, Texte)
- **StreamerBot-Integration**: Automatische Auslösung von Aktionen bei Gewinn
- **WebSocket-Anbindung** für externe Steuerung
- **Zwei Rad-Modi**: Segmentgrößen nach Wahrscheinlichkeit oder gleich groß
- **Konfigurierbare Animationen** und Blinkeffekte für Gewinnersegmente
- **Einfache Konfiguration über settings.html**
- **Speicherung der letzten Rad-Position (localStorage)**
- **Moderne, übersichtliche Oberfläche für Einstellungen**

## Projektstruktur

- `index.html` – Hauptanwendung (Glücksrad)
- `settings.html` – Einstellungsseite für das Rad und die Segmente
- `main.css` – Styles für beide Seiten
- `config.js` – Konfigurationsdatei für das Rad (wird von beiden Seiten genutzt)
- `Winwheel.js` – Glücksrad-Logik (externe Bibliothek)
- `image/` – Bilder für Segmente (optional)

## Start & Nutzung

1. **Lokale Nutzung**
   - Öffne `index.html` für das Glücksrad
   - Öffne `settings.html` für die Einstellungen

> Wichtig: Die Seite muss im selben Netzwerk wie StreamerBot laufen (WebSocket auf `ws://127.0.0.1:8080`).

## Konfiguration

### Einstellungen über settings.html
- **StreamerBot**: Trage die Action-ID ein, die bei einem Gewinn ausgelöst werden soll.
- **Rad-Einstellungen**: Modus, Größe, Farben, Animationen, Pins, Textoptionen etc.
- **Segmente**: Füge beliebig viele Segmente hinzu, passe Wahrscheinlichkeiten, Farben, Texte und Bilder an.
- **Konfiguration exportieren/importieren**: Speichere deine Einstellungen als Datei oder lade sie wieder.

### Manuelle Konfiguration (config.js)
- Passe die Werte in `config.js` direkt an, z.B. für:
  - Action-ID (`ACTION_ID`)
  - Blink-Einstellungen (`blinkConfig`)
  - Rad-Modus (`wheelMode`)
  - Segmente (`priceSegment`)

## StreamerBot-Integration

- Das Glücksrad lauscht auf WebSocket-Events von StreamerBot.
- Sende das Custom-Event `glücksradStart` an den WebSocket, um das Rad zu starten.
- Die Action-ID aus den Einstellungen wird bei Gewinn ausgelöst.

## Hinweise

- Für die WebSocket-Anbindung muss StreamerBot auf dem Standardport laufen.
- Die Segmentbilder können als Base64 oder als Pfad im `image/`-Ordner angegeben werden.

## OBS Studio Einbindung

1. Öffnen Sie OBS Studio
2. Klicken Sie auf das "+" unter "Quellen"
3. Wählen Sie "Browser" aus
4. Geben Sie einen Namen ein (z.B. "Glücksrad")
5. Aktivieren Sie "Lokale Datei"
6. Klicken Sie auf "Durchsuchen" und wählen Sie die `index.html` aus
7. Setzen Sie die Breite und Höhe auf 800x800 Pixel (oder Ihre gewünschte Größe)
8. Klicken Sie auf "OK"

> Tipp: Aktivieren Sie "Aktualisieren Sie den Browser, wenn die Szene aktiv wird" für eine bessere Performance.

## Lizenz

MIT License

---

**Viel Spaß beim Drehen!**