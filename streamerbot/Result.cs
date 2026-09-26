// Action "Glücksrad – Ergebnis"
// Wird von der Browser-Quelle aufgerufen, sobald das Rad stehen bleibt. Nicht manuell auslösen.
using System;
using System.Collections.Generic;
using System.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string spinId = GrCore.ArgString(args, "grSpinId");
        if (spinId == null) return false;

        // Nur das erste Ergebnis pro Drehung zählt, und das Feld kommt aus dem gespeicherten Los
        string pending = CPH.GetGlobalVar<string>(GrCore.PendingPrefix + spinId, false);
        if (string.IsNullOrEmpty(pending)) return true;
        CPH.UnsetGlobalVar(GrCore.PendingPrefix + spinId, false);

        string[] parts = pending.Split(new[] { '|' }, 4);
        if (parts.Length < 4) return false;
        string wheelId = parts[0];
        int idx = int.Parse(parts[1]);
        bool test = parts[2] == "1";
        string user = parts[3];

        GrConfig cfg = GrCore.Load(CPH);
        GrWheel wheel = cfg.wheels.FirstOrDefault(w => w.id == wheelId);
        if (wheel == null || idx < 0 || idx >= wheel.segments.Count) return false;
        GrSegment seg = wheel.segments[idx];

        CPH.SetArgument("gluecksradUser", user);
        CPH.SetArgument("gluecksradWheel", wheel.name);
        CPH.SetArgument("gluecksradPrize", seg.text);
        CPH.SetArgument("gluecksradField", idx + 1);
        CPH.SetArgument("gluecksradTest", test);
        CPH.SetGlobalVar("gluecksradLastUser", user, false);
        CPH.SetGlobalVar("gluecksradLastPrize", seg.text, false);

        if (!string.IsNullOrEmpty(seg.chat))
        {
            string msg = seg.chat.Replace("%user%", user).Replace("%prize%", seg.text).Replace("%wheel%", wheel.name);
            if (test) msg = "[Test] " + msg;
            CPH.SendMessage(msg, cfg.chatAsBot, true);
        }

        if (!string.IsNullOrEmpty(seg.action))
        {
            if (test) CPH.LogInfo("[Glücksrad] Test-Drehung: Action '" + seg.action + "' wird nicht ausgeführt.");
            else
            {
                // "user" überschreiben, damit die Gewinn-Action mit %user% direkt den Gewinner kennt
                CPH.SetArgument("user", user);
                CPH.RunAction(seg.action, true);
            }
        }

        if (seg.freeSpin) GrCore.StartSpin(CPH, wheel, user, test);
        return true;
    }
}
