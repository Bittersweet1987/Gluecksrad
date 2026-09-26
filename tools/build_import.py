"""Baut den Streamer.bot-Import-String und die zusammengesetzten C#-Dateien.

Aufruf:  python tools/build_import.py
Ergebnis:
  dist/Gluecksrad_Import.txt   -> in Streamer.bot über "Import" einfügen
  dist/*.cs                    -> fertiger Code je Action (zum Nachschauen oder manuellen Einfügen)
"""
import base64
import gzip
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "streamerbot"
DIST = ROOT / "dist"
VERSION = "2.0.0"
GROUP = "Glücksrad"

FW = "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\"
BASE_REFS = [FW + "mscorlib.dll", FW + "System.dll", FW + "System.Core.dll", FW + "System.Web.Extensions.dll"]
GUI_REFS = [FW + "System.Windows.Forms.dll", FW + "System.Drawing.dll"]

# Feste IDs, damit ein erneuter Import die bestehenden Actions ersetzt (Overlay kennt die Ergebnis-ID).
ACTIONS = [
    ("f1a2b3c4-1111-4a6b-9c0d-5e6f7a8b9c01", "Glücksrad – Einstellungen", "Settings.cs", True),
    ("f1a2b3c4-2222-4a6b-9c0d-5e6f7a8b9c02", "Glücksrad – Drehen", "Spin.cs", False),
    ("a7f3c2e1-5b4d-4e8a-9c1f-2d3e4f5a6b72", "Glücksrad – Ergebnis", "Result.cs", False),
]

USING_RE = re.compile(r"^\s*using\s+[\w.]+\s*;\s*$", re.M)


def split_usings(code: str):
    usings = [u.strip() for u in USING_RE.findall(code)]
    return usings, USING_RE.sub("", code).strip() + "\n"


def csharp_verbatim(text: str) -> str:
    return '@"' + text.replace('"', '""') + '"'


def combine(action_file: str) -> str:
    shared_usings, shared_body = split_usings((SRC / "Shared.cs").read_text(encoding="utf-8"))
    action_usings, action_body = split_usings((SRC / action_file).read_text(encoding="utf-8"))
    if action_file == "Settings.cs":
        overlay = (ROOT / "overlay" / "overlay.html").read_text(encoding="utf-8")
        marker = 'const string OverlayTemplate = ""; //GR_OVERLAY_TEMPLATE'
        assert marker in action_body, "Platzhalter für Overlay fehlt in Settings.cs"
        action_body = action_body.replace(marker, "const string OverlayTemplate = " + csharp_verbatim(overlay) + ";")
    usings = []
    for u in action_usings + shared_usings:
        if u not in usings:
            usings.append(u)
    # Kommentare am Dateianfang der Shared.cs entfernen (nur die Action-Beschreibung bleibt oben)
    shared_body = re.sub(r"^(//[^\n]*\n)+", "", shared_body)
    return ("\n".join(usings) + "\n\n" + action_body + "\n// ===== Gemeinsamer Glücksrad-Code =====\n" + shared_body)


def sub_action(action_id: str, name: str, code: str, gui: bool, index: int):
    return {
        "name": name,
        "description": "Glücksrad " + VERSION,
        "references": BASE_REFS + (GUI_REFS if gui else []),
        "byteCode": base64.b64encode(code.encode("utf-8")).decode("ascii"),
        "precompile": False,
        "delayStart": False,
        "saveResultToVariable": False,
        "saveToVariable": "",
        "id": action_id[:-4] + "c0de",
        "weight": 0.0,
        "type": 99999,
        "parentId": None,
        "enabled": True,
        "index": index,
    }


def main():
    DIST.mkdir(exist_ok=True)
    actions = []
    for action_id, name, file, gui in ACTIONS:
        code = combine(file)
        (DIST / file).write_text(code, encoding="utf-8")
        actions.append({
            "id": action_id,
            "queue": "00000000-0000-0000-0000-000000000000",
            "enabled": True,
            "excludeFromHistory": False,
            "excludeFromPending": False,
            "name": name,
            "group": GROUP,
            "alwaysRun": False,
            "randomAction": False,
            "concurrent": True,
            "triggers": [],
            "subActions": [sub_action(action_id, name, code, gui, 0)],
            "collapsedGroups": [],
        })

    export = {
        "meta": {
            "name": "Glücksrad",
            "author": "Bittersweet1987",
            "version": VERSION,
            "description": "Glücksrad für OBS mit Einstellungsfenster. Nach dem Import die Action "
                           "„Glücksrad – Einstellungen“ ausführen.",
            "autoRunAction": None,
            "minimumVersion": None,
        },
        "data": {
            "actions": actions,
            "queues": [],
            "commands": [],
            "websocketServers": [],
            "websocketClients": [],
            "timers": [],
        },
    }
    raw = json.dumps(export, ensure_ascii=False).encode("utf-8")
    encoded = base64.b64encode(b"SBAE" + gzip.compress(raw)).decode("ascii")
    (DIST / "Gluecksrad_Import.txt").write_text(encoded, encoding="ascii")
    print("dist/Gluecksrad_Import.txt geschrieben (%d Zeichen)" % len(encoded))


if __name__ == "__main__":
    main()
