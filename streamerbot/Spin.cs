// Action "Glücksrad – Drehen"
// Hänge hier alle Trigger an (Kanalpunkte-Belohnung, Cheer, Sub, Gift, Tipps, Chat-Befehl).
// Welches Rad sich dreht, wird im Fenster "Glücksrad – Einstellungen" festgelegt.
using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
        GrConfig cfg = GrCore.Load(CPH);
        string reason;
        GrWheel wheel = GrCore.FindWheel(cfg, args, out reason);
        if (wheel == null)
        {
            CPH.LogInfo("[Glücksrad] Kein Rad für dieses Ereignis eingestellt (" + reason + ").");
            return true;
        }
        GrCore.StartSpin(CPH, wheel, GrCore.FindUser(args), false);
        return true;
    }
}
