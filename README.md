# Glücksrad

Ein anpassbares Glücksrad für Streams, Events oder Gewinnspiele – mit Integration für StreamerBot und umfangreichen Einstellungsmöglichkeiten.

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

## Installation & Start

1. **Repository klonen**
   ```
   git clone https://github.com/Bittersweet1987/Gluecksrad.git
   ```
2. **Dateien auf einen Webserver oder lokal öffnen**
   - Öffne `index.html` für das Glücksrad
   - Öffne `settings.html` für die Einstellungen

> Für die StreamerBot-Integration muss die Seite im selben Netzwerk wie StreamerBot laufen (WebSocket auf `ws://127.0.0.1:8080`).

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

## Nutzung

- **Glücksrad starten**: Über einen StreamerBot-Event (z.B. Chatbefehl) oder direkt im Browser.
- **Gewinnerauswahl**: Zufällig, basierend auf Segmentwahrscheinlichkeiten oder gleichverteilt.
- **Aktionen**: Bei Gewinn wird die konfigurierte StreamerBot-Action ausgelöst.

## StreamerBot-Integration

- Das Glücksrad lauscht auf WebSocket-Events von StreamerBot.
- Sende das Custom-Event `glücksradStart` an den WebSocket, um das Rad zu starten.
- Die Action-ID aus den Einstellungen wird bei Gewinn ausgelöst.

## Hinweise

- Die Anwendung funktioniert am besten in aktuellen Browsern mit aktiviertem JavaScript.
- Für die WebSocket-Anbindung muss StreamerBot auf dem Standardport laufen.
- Die Segmentbilder können als Base64 oder als Pfad im `image/`-Ordner angegeben werden.

## Lizenz

MIT License

---

**Viel Spaß beim Drehen!**