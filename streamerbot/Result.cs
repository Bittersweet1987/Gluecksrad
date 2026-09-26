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

        var data = GrCore.Json().DeserializeObject(pending) as Dictionary<string, object>;
        if (data == null) return false;
        string wheelId = Convert.ToString(data["wheelId"]);
        int idx = Convert.ToInt32(data["index"]);
        bool test = Convert.ToBoolean(data["test"]);
        string user = Convert.ToString(data["user"]);
        string amount = data.ContainsKey("amount") ? Convert.ToString(data["amount"]) : "";
        string trigger = data.ContainsKey("trigger") ? Convert.ToString(data["trigger"]) : "";

        GrConfig cfg = GrCore.Load(CPH);
        GrWheel wheel = cfg.wheels.FirstOrDefault(w => w.id == wheelId);
        if (wheel == null || idx < 0 || idx >= wheel.segments.Count) return false;
        GrSegment seg = wheel.segments[idx];

        CPH.SetArgument("gluecksradUser", user);
        CPH.SetArgument("gluecksradWheel", wheel.name);
        CPH.SetArgument("gluecksradPrize", seg.text);
        CPH.SetArgument("gluecksradField", idx + 1);
        CPH.SetArgument("gluecksradTest", test);
        CPH.SetArgument("gluecksradAmount", amount);
        CPH.SetArgument("gluecksradTrigger", trigger);
        CPH.SetGlobalVar("gluecksradLastUser", user, false);
        CPH.SetGlobalVar("gluecksradLastPrize", seg.text, false);

        if (!string.IsNullOrEmpty(seg.chat))
        {
            var values = new Dictionary<string, string>();
            values["%user%"] = user;
            values["%prize%"] = seg.text;
            values["%wheel%"] = wheel.name;
            values["%amount%"] = amount;
            values["%trigger%"] = trigger;
            values["%field%"] = (idx + 1).ToString();
            string msg = GrCore.FillText(seg.chat, values);
            if (test) msg = "[Test] " + msg;
            CPH.SendMessage(msg, cfg.chatAsBot, true);
        }

        if (!string.IsNullOrEmpty(seg.action))
        {
            if (test) CPH.LogInfo("[Glücksrad] Test-Drehung: Action '" + seg.action + "' wird nicht ausgeführt.");
            else
            {
                // "user" überschreiben, damit die Gewinn-Action mit %user% direkt den Gewinner kennt.
                // Ältere Roulette-Actions lesen den Gewinner aus den globalen Variablen user/rouletteWin.
                CPH.SetArgument("user", user);
                CPH.SetGlobalVar("user", user, true);
                CPH.SetGlobalVar("rouletteWin", seg.text, true);
                CPH.RunAction(seg.action, true);
            }
        }

        if (seg.freeSpin) GrCore.StartSpin(CPH, wheel, user, test, -1, "", "Freispin");
        return true;
    }
}
