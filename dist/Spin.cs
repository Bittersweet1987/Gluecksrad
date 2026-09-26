using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Streamer.bot.Plugin.Interface;

// Action "Glücksrad – Drehen"
// Hänge hier alle Trigger an (Kanalpunkte-Belohnung, Cheer, Sub, Gift, Tipps, Chat-Befehl).
// Welches Rad sich dreht, wird im Fenster "Glücksrad – Einstellungen" festgelegt.


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
        int field = (int)GrCore.ArgNumber(args, "gluecksradForceField");
        string trigger, amount;
        GrCore.DescribeTrigger(args, out trigger, out amount);
        GrCore.StartSpin(CPH, wheel, GrCore.FindUser(args), false, field - 1, amount, trigger);
        return true;
    }
}

// ===== Gemeinsamer Glücksrad-Code =====








public class GrSegment
{
    public string text { get; set; }
    public string color { get; set; }
    public string textColor { get; set; }
    public string image { get; set; }      // Data-URI oder leer
    public double weight { get; set; }     // Gewichtung / Chance
    public string chat { get; set; }       // Chatnachricht, %user% und %prize% werden ersetzt
    public string action { get; set; }     // Name einer Streamer.bot-Action oder leer
    public string sound { get; set; }      // Data-URI oder leer
    public bool freeSpin { get; set; }

    public GrSegment()
    {
        text = "Neues Feld"; color = "#3369e8"; textColor = "#ffffff"; image = ""; weight = 10;
        chat = "%user% hat %prize% gewonnen!"; action = ""; sound = ""; freeSpin = false;
    }
}

public class GrTriggers
{
    public List<string> rewardIds { get; set; }
    public bool bits { get; set; }
    public int bitsMin { get; set; }
    public bool subs { get; set; }
    public bool resubs { get; set; }
    public bool giftSubs { get; set; }
    public int giftMin { get; set; }
    public bool tips { get; set; }
    public double tipMin { get; set; }
    public string command { get; set; }

    public GrTriggers()
    {
        rewardIds = new List<string>(); bits = false; bitsMin = 500; subs = false; resubs = false;
        giftSubs = false; giftMin = 1; tips = false; tipMin = 5; command = "";
    }
}

public class GrLook
{
    public double spinSeconds { get; set; }
    public int spins { get; set; }
    public string borderColor { get; set; }
    public int borderWidth { get; set; }
    public string lineColor { get; set; }      // Trennlinien zwischen den Feldern
    public int lineWidth { get; set; }
    public string pointerColor { get; set; }
    public bool showPointer { get; set; }
    public string centerColor { get; set; }
    public string centerImage { get; set; }    // Data-URI oder leer
    public int hubSize { get; set; }           // Nabe in % des Radius (nur bei vollem Rad)
    public int innerRadius { get; set; }       // hohle Mitte in % des Radius (0 = volles Rad)
    public bool proportional { get; set; }
    public int fontSize { get; set; }
    public bool showText { get; set; }
    public string imageMode { get; set; }      // inward, outward, left, right, upright
    public int imageSize { get; set; }         // Bildlänge in % des Radius
    public int imageDistance { get; set; }     // Abstand der Bild-Innenkante von der Mitte in % des Radius
    public string winColor { get; set; }
    public int blinkMs { get; set; }
    public bool showBanner { get; set; }
    public double holdSeconds { get; set; }
    public bool idleVisible { get; set; }
    public bool tick { get; set; }
    public double volume { get; set; }

    public GrLook()
    {
        spinSeconds = 12; spins = 8; borderColor = "#ffffff"; borderWidth = 6; lineColor = "#ffffff"; lineWidth = 2;
        pointerColor = "#ffcc00"; showPointer = true; centerColor = "#ffffff"; centerImage = ""; hubSize = 11;
        innerRadius = 0; proportional = true; fontSize = 28; showText = true;
        imageMode = "outward"; imageSize = 40; imageDistance = 40;
        winColor = "#19cfe5"; blinkMs = 300; showBanner = true; holdSeconds = 6;
        idleVisible = false; tick = true; volume = 0.5;
    }
}

public class GrObs
{
    public int width { get; set; }
    public int height { get; set; }
    public string addToScene { get; set; }     // optional: Szene, in die das Rad als verschachtelte Szene kommt
    public string syncedScene { get; set; }    // zuletzt in OBS angelegter Szenenname (für Umbenennen)
    public string syncedInput { get; set; }    // zuletzt in OBS angelegter Quellenname (für Umbenennen)

    public GrObs() { width = 1080; height = 1080; addToScene = ""; syncedScene = ""; syncedInput = ""; }
}

public class GrWheel
{
    public string id { get; set; }
    public string name { get; set; }
    public bool enabled { get; set; }
    public List<GrSegment> segments { get; set; }
    public GrTriggers triggers { get; set; }
    public GrLook look { get; set; }
    public GrObs obs { get; set; }

    public GrWheel()
    {
        id = Guid.NewGuid().ToString("N").Substring(0, 12); name = "Neues Rad"; enabled = true;
        segments = new List<GrSegment>(); triggers = new GrTriggers(); look = new GrLook(); obs = new GrObs();
    }
}

public class GrConfig
{
    public int version { get; set; }
    public string host { get; set; }
    public int port { get; set; }
    public int obsConnection { get; set; }
    public bool chatAsBot { get; set; }
    public bool darkMode { get; set; }
    public List<GrWheel> wheels { get; set; }

    public GrConfig() { version = 1; host = "127.0.0.1"; port = 8080; obsConnection = 0; chatAsBot = true; darkMode = true; wheels = new List<GrWheel>(); }
}

public static class GrCore
{
    public const string ConfigVar = "gluecksrad_config";
    public const string PendingPrefix = "gluecksrad_pending_";
    public const string SettingsActionName = "Glücksrad – Einstellungen";
    public const string SpinActionName = "Glücksrad – Drehen";
    public const string ResultActionName = "Glücksrad – Ergebnis";
    public const string ResultActionId = "a7f3c2e1-5b4d-4e8a-9c1f-2d3e4f5a6b72";

    static readonly Random rng = new Random();

    public static JavaScriptSerializer Json()
    {
        var s = new JavaScriptSerializer();
        s.MaxJsonLength = int.MaxValue;
        return s;
    }

    // ---------- Config ----------
    public static GrConfig Load(IInlineInvokeProxy cph)
    {
        GrConfig cfg = null;
        try
        {
            string raw = cph.GetGlobalVar<string>(ConfigVar, true);
            if (!string.IsNullOrEmpty(raw)) cfg = Json().Deserialize<GrConfig>(raw);
        }
        catch (Exception ex) { cph.LogWarn("[Glücksrad] Config konnte nicht gelesen werden: " + ex.Message); }
        if (cfg == null) cfg = new GrConfig();
        Normalize(cfg);
        return cfg;
    }

    public static void Save(IInlineInvokeProxy cph, GrConfig cfg)
    {
        Normalize(cfg);
        cph.SetGlobalVar(ConfigVar, Json().Serialize(cfg), true);
    }

    public static void Normalize(GrConfig cfg)
    {
        if (cfg.wheels == null) cfg.wheels = new List<GrWheel>();
        if (string.IsNullOrEmpty(cfg.host)) cfg.host = "127.0.0.1";
        if (cfg.port <= 0) cfg.port = 8080;
        foreach (var w in cfg.wheels)
        {
            if (string.IsNullOrEmpty(w.id)) w.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            if (w.name == null) w.name = "Rad";
            if (w.segments == null) w.segments = new List<GrSegment>();
            if (w.triggers == null) w.triggers = new GrTriggers();
            if (w.triggers.rewardIds == null) w.triggers.rewardIds = new List<string>();
            if (w.triggers.command == null) w.triggers.command = "";
            if (w.look == null) w.look = new GrLook();
            if (w.look.centerImage == null) w.look.centerImage = "";
            if (string.IsNullOrEmpty(w.look.imageMode)) w.look.imageMode = "outward";
            if (string.IsNullOrEmpty(w.look.lineColor)) w.look.lineColor = w.look.borderColor ?? "#ffffff";
            if (string.IsNullOrEmpty(w.look.winColor)) w.look.winColor = "#19cfe5";
            if (w.obs == null) w.obs = new GrObs();
            if (w.obs.addToScene == null) w.obs.addToScene = "";
            if (w.obs.syncedScene == null) w.obs.syncedScene = "";
            if (w.obs.syncedInput == null) w.obs.syncedInput = "";
            foreach (var s in w.segments)
            {
                if (s.text == null) s.text = "";
                if (s.chat == null) s.chat = "";
                if (s.action == null) s.action = "";
                if (s.image == null) s.image = "";
                if (s.sound == null) s.sound = "";
                if (string.IsNullOrEmpty(s.color)) s.color = "#888888";
                if (string.IsNullOrEmpty(s.textColor)) s.textColor = "#000000";
            }
        }
    }

    public static GrWheel ExampleWheel()
    {
        var w = new GrWheel();
        w.name = "Kanalpunkte";
        w.segments.Add(Seg("Geh aufs Ganze", "#009925", "#ffffff", 14, "%user% gewinnt eine Runde 'Geh aufs Ganze'!", false));
        w.segments.Add(Seg("Zonk", "#d50f25", "#ffffff", 12, "%user% hat leider einen Zonk gedreht.", false));
        w.segments.Add(Seg("Freispin", "#ffff99", "#000000", 15, "%user% gewinnt einen Freispin. Viel Glück!", true));
        w.segments.Add(Seg("Zonk", "#d50f25", "#ffffff", 12, "%user% hat leider einen Zonk gedreht.", false));
        w.segments.Add(Seg("Pushups", "#009925", "#ffffff", 10, "%user% schenkt dem Streamer 5 Pushups!", false));
        w.segments.Add(Seg("Zonk", "#d50f25", "#ffffff", 13, "%user% hat leider einen Zonk gedreht.", false));
        w.segments.Add(Seg("10.000 Punkte", "#ffff99", "#000000", 10, "%user% gewinnt 10.000 Punkte!", false));
        w.segments.Add(Seg("Zonk", "#d50f25", "#ffffff", 14, "%user% hat leider einen Zonk gedreht.", false));
        return w;
    }

    static GrSegment Seg(string text, string color, string textColor, double weight, string chat, bool freeSpin)
    {
        var s = new GrSegment();
        s.text = text; s.color = color; s.textColor = textColor; s.weight = weight; s.chat = chat; s.freeSpin = freeSpin;
        return s;
    }

    // ---------- Rad exportieren / importieren ----------
    public static string ExportWheel(GrWheel w)
    {
        var d = new Dictionary<string, object>();
        d["gluecksradWheel"] = 1;
        d["wheel"] = w;
        return Json().Serialize(d);
    }

    public static GrWheel ImportWheel(string json)
    {
        var s = Json();
        var raw = s.DeserializeObject(json) as Dictionary<string, object>;
        if (raw == null) throw new Exception("Keine gültige Glücksrad-Datei.");
        object inner;
        string wheelJson = raw.TryGetValue("wheel", out inner) ? s.Serialize(inner) : json;
        var w = s.Deserialize<GrWheel>(wheelJson);
        if (w == null || w.segments == null) throw new Exception("Keine gültige Glücksrad-Datei.");
        var tmp = new GrConfig();
        tmp.wheels.Add(w);
        Normalize(tmp);
        return w;
    }

    // ---------- Namen / Dateien ----------
    public static string ObsSceneName(GrWheel w) { return "Glücksrad – " + w.name; }
    public static string ObsInputName(GrWheel w) { return "Glücksrad – " + w.name + " (Rad)"; }

    public static string OverlayDir()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gluecksrad");
    }

    public static string OverlayPath(GrWheel w)
    {
        return Path.Combine(OverlayDir(), "rad_" + w.id + ".html");
    }

    public static string BuildOverlayHtml(string template, GrConfig cfg, GrWheel w, string resultActionId)
    {
        var boot = new Dictionary<string, object>();
        boot["wheelId"] = w.id;
        boot["host"] = cfg.host;
        boot["port"] = cfg.port.ToString(CultureInfo.InvariantCulture);
        boot["resultActionId"] = resultActionId;
        boot["resultActionName"] = ResultActionName;
        // Nur optische Daten ins Overlay: Auslöser, Chattexte usw. sollen kein Neuladen der OBS-Quelle auslösen
        var visual = new Dictionary<string, object>();
        visual["id"] = w.id;
        visual["name"] = w.name;
        visual["look"] = w.look;
        visual["segments"] = w.segments.Select(sg => new Dictionary<string, object>
        {
            { "text", sg.text }, { "color", sg.color }, { "textColor", sg.textColor },
            { "image", sg.image }, { "weight", sg.weight }, { "sound", sg.sound }
        }).ToList();
        boot["wheel"] = visual;
        string json = Json().Serialize(boot).Replace("</", "<\\/");
        return template.Replace("/*GR_BOOT*/null/*GR_BOOT_END*/", "/*GR_BOOT*/" + json + "/*GR_BOOT_END*/");
    }

    // ---------- Drehen ----------
    public static int PickSegment(GrWheel w)
    {
        var segs = w.segments;
        double total = segs.Sum(s => Math.Max(0, s.weight));
        if (total <= 0) return rng.Next(segs.Count);
        double r = rng.NextDouble() * total;
        double acc = 0;
        for (int i = 0; i < segs.Count; i++)
        {
            acc += Math.Max(0, segs[i].weight);
            if (r < acc) return i;
        }
        return segs.Count - 1;
    }

    // Lost ein Feld aus und schickt das Spin-Event an die Browser-Quelle(n) des Rads.
    // Variablen für Chatnachrichten: Platzhalter, Knopftext, Beschreibung
    public static readonly string[][] ChatVariables =
    {
        new[] { "%user%", "Gewinner", "Name des Gewinners" },
        new[] { "%prize%", "Gewinn", "Text des Gewinnfeldes" },
        new[] { "%wheel%", "Rad", "Name des Rads" },
        new[] { "%amount%", "Betrag", "Bits, Spendenbetrag, Anzahl Gift-Subs bzw. Kanalpunkte-Kosten" },
        new[] { "%trigger%", "Auslöser", "Was die Drehung ausgelöst hat, z. B. Kanalpunkte, Bits, Spende, Freispin" },
        new[] { "%field%", "Feldnummer", "Nummer des Gewinnfeldes" }
    };

    public static string FillText(string text, Dictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text)) return "";
        foreach (var kv in values) text = text.Replace(kv.Key, kv.Value ?? "");
        return text;
    }

    public static void StartSpin(IInlineInvokeProxy cph, GrWheel w, string user, bool test)
    {
        StartSpin(cph, w, user, test, -1, "", "Test");
    }

    // forcedIndex >= 0 legt das Gewinnfeld fest (nur zum Testen, Argument "gluecksradForceField" = Feldnummer ab 1)
    public static void StartSpin(IInlineInvokeProxy cph, GrWheel w, string user, bool test, int forcedIndex, string amount, string trigger)
    {
        if (w.segments.Count == 0) { cph.LogWarn("[Glücksrad] Rad '" + w.name + "' hat keine Felder."); return; }
        int idx = forcedIndex >= 0 && forcedIndex < w.segments.Count ? forcedIndex : PickSegment(w);
        string spinId = Guid.NewGuid().ToString("N");
        // Ergebnis serverseitig merken: nur das erste Ergebnis-Event zählt (z.B. bei doppelt geladener Quelle)
        var pending = new Dictionary<string, object>();
        pending["wheelId"] = w.id;
        pending["index"] = idx;
        pending["test"] = test;
        pending["user"] = user;
        pending["amount"] = amount ?? "";
        pending["trigger"] = trigger ?? "";
        cph.SetGlobalVar(PendingPrefix + spinId, Json().Serialize(pending), false);

        var payload = new Dictionary<string, object>();
        payload["source"] = "gluecksrad";
        payload["event"] = "spin";
        payload["wheelId"] = w.id;
        payload["spinId"] = spinId;
        payload["user"] = user;
        payload["targetIndex"] = idx;
        payload["wheel"] = w;
        cph.WebsocketBroadcastJson(Json().Serialize(payload));
        cph.LogInfo("[Glücksrad] Drehe '" + w.name + "' für " + user + " → Feld " + (idx + 1) + " (" + w.segments[idx].text + ")");
    }

    public static void BroadcastPreview(IInlineInvokeProxy cph, GrWheel w)
    {
        var payload = new Dictionary<string, object>();
        payload["source"] = "gluecksrad";
        payload["event"] = "preview";
        payload["wheelId"] = w.id;
        payload["wheel"] = w;
        cph.WebsocketBroadcastJson(Json().Serialize(payload));
    }

    // ---------- Argumente ----------
    public static string ArgString(Dictionary<string, object> args, params string[] keys)
    {
        foreach (var k in keys)
        {
            object v;
            if (args.TryGetValue(k, out v) && v != null && v.ToString().Trim() != "") return v.ToString();
        }
        return null;
    }

    public static double ArgNumber(Dictionary<string, object> args, params string[] keys)
    {
        foreach (var k in keys)
        {
            object v;
            if (args.TryGetValue(k, out v) && v != null)
            {
                double d;
                if (TryParseNumber(v.ToString(), out d)) return d;
            }
        }
        return 0;
    }

    public static bool ArgBool(Dictionary<string, object> args, string key)
    {
        object v;
        if (!args.TryGetValue(key, out v) || v == null) return false;
        bool b;
        return bool.TryParse(v.ToString(), out b) && b;
    }

    public static bool TryParseNumber(string s, out double d)
    {
        s = (s ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
    }

    // Welches Rad gehört zum auslösenden Ereignis?
    public static GrWheel FindWheel(GrConfig cfg, Dictionary<string, object> args, out string reason)
    {
        reason = "";
        var wheels = cfg.wheels.Where(x => x.enabled && x.segments.Count > 0).ToList();

        // 1) Manuell per Argument "wheel" (Name oder ID), z.B. über "Set Argument"
        string manual = ArgString(args, "wheel", "gluecksrad");
        if (manual != null)
        {
            var m = wheels.FirstOrDefault(x => x.id == manual || string.Equals(x.name, manual, StringComparison.OrdinalIgnoreCase));
            reason = "Argument wheel=" + manual;
            return m;
        }

        string source = ArgString(args, "__source") ?? "";
        reason = "Auslöser " + source;

        // 2) Kanalpunkte
        string rewardId = ArgString(args, "rewardId");
        if (rewardId != null)
            return wheels.FirstOrDefault(x => x.triggers.rewardIds.Contains(rewardId));

        // 3) Chat-Befehl
        string command = ArgString(args, "command");
        if (command != null && source.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0)
            return wheels.FirstOrDefault(x => x.triggers.command.Trim() != "" &&
                string.Equals(x.triggers.command.Trim(), command.Trim(), StringComparison.OrdinalIgnoreCase));

        // 4) Bits
        if (source.IndexOf("Cheer", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            double bits = ArgNumber(args, "bits");
            return wheels.Where(x => x.triggers.bits && bits >= x.triggers.bitsMin)
                         .OrderByDescending(x => x.triggers.bitsMin).FirstOrDefault();
        }

        // 5) Gift-Subs (Gift Bomb zählt als ein Ereignis, die einzelnen Geschenke daraus werden ignoriert)
        if (source.IndexOf("GiftBomb", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            double gifts = ArgNumber(args, "gifts");
            return wheels.Where(x => x.triggers.giftSubs && gifts >= x.triggers.giftMin)
                         .OrderByDescending(x => x.triggers.giftMin).FirstOrDefault();
        }
        if (source.IndexOf("GiftSub", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (ArgBool(args, "fromSubBomb") || ArgBool(args, "fromGiftBomb")) { reason += " (Teil einer Gift Bomb)"; return null; }
            return wheels.FirstOrDefault(x => x.triggers.giftSubs && x.triggers.giftMin <= 1);
        }
        if (source.IndexOf("ReSub", StringComparison.OrdinalIgnoreCase) >= 0)
            return wheels.FirstOrDefault(x => x.triggers.resubs);
        if (source.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0)
            return wheels.FirstOrDefault(x => x.triggers.subs);

        // 6) Tipps / Spenden (StreamElements, Streamlabs, Ko-fi, Tipeee, ...)
        if (source.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source.IndexOf("Donation", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            double amount = ArgNumber(args, "tipAmount", "donationAmount", "amount");
            return wheels.Where(x => x.triggers.tips && amount >= x.triggers.tipMin)
                         .OrderByDescending(x => x.triggers.tipMin).FirstOrDefault();
        }
        return null;
    }

    // Beschreibt das auslösende Ereignis für %trigger% und %amount%
    public static void DescribeTrigger(Dictionary<string, object> args, out string trigger, out string amount)
    {
        string source = ArgString(args, "__source") ?? "";
        trigger = "Manuell"; amount = "";
        Func<double, string> fmt = d => d.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
        if (ArgString(args, "rewardId") != null) { trigger = "Kanalpunkte"; double c = ArgNumber(args, "rewardCost"); amount = c > 0 ? fmt(c) : ""; }
        else if (source.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0) trigger = "Befehl";
        else if (source.IndexOf("Cheer", StringComparison.OrdinalIgnoreCase) >= 0) { trigger = "Bits"; amount = fmt(ArgNumber(args, "bits")); }
        else if (source.IndexOf("GiftBomb", StringComparison.OrdinalIgnoreCase) >= 0) { trigger = "Gift-Subs"; amount = fmt(ArgNumber(args, "gifts")); }
        else if (source.IndexOf("GiftSub", StringComparison.OrdinalIgnoreCase) >= 0) { trigger = "Gift-Sub"; amount = "1"; }
        else if (source.IndexOf("ReSub", StringComparison.OrdinalIgnoreCase) >= 0) trigger = "Resub";
        else if (source.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) >= 0) trigger = "Sub";
        else if (source.IndexOf("Tip", StringComparison.OrdinalIgnoreCase) >= 0 || source.IndexOf("Donation", StringComparison.OrdinalIgnoreCase) >= 0)
        { trigger = "Spende"; amount = fmt(ArgNumber(args, "tipAmount", "donationAmount", "amount")); }
    }

    public static string FindUser(Dictionary<string, object> args)
    {
        return ArgString(args, "gluecksradUser", "user", "tipUsername", "donationFrom", "from", "userName", "name") ?? "Jemand";
    }

    // ---------- OBS (obs-websocket v5 über Streamer.bot) ----------
    public static Dictionary<string, object> ObsRequest(IInlineInvokeProxy cph, GrConfig cfg, string type, Dictionary<string, object> data)
    {
        string raw = cph.ObsSendRaw(type, Json().Serialize(data ?? new Dictionary<string, object>()), cfg.obsConnection);
        if (string.IsNullOrEmpty(raw)) return new Dictionary<string, object>();
        try
        {
            var obj = Json().DeserializeObject(raw) as Dictionary<string, object>;
            return obj ?? new Dictionary<string, object>();
        }
        catch { return new Dictionary<string, object>(); }
    }

    static Dictionary<string, object> D(params object[] kv)
    {
        var d = new Dictionary<string, object>();
        for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
        return d;
    }

    static List<string> NamesFrom(Dictionary<string, object> resp, string listKey, string nameKey)
    {
        var result = new List<string>();
        object list;
        if (resp.TryGetValue(listKey, out list) && list is System.Collections.IEnumerable)
        {
            foreach (var o in (System.Collections.IEnumerable)list)
            {
                var d = o as Dictionary<string, object>;
                object n;
                if (d != null && d.TryGetValue(nameKey, out n) && n != null) result.Add(n.ToString());
            }
        }
        return result;
    }

    public static List<string> ObsScenes(IInlineInvokeProxy cph, GrConfig cfg)
    {
        return NamesFrom(ObsRequest(cph, cfg, "GetSceneList", null), "scenes", "sceneName");
    }

    public static List<string> ObsInputs(IInlineInvokeProxy cph, GrConfig cfg)
    {
        return NamesFrom(ObsRequest(cph, cfg, "GetInputList", null), "inputs", "inputName");
    }

    // Legt Szene + Browser-Quelle an oder aktualisiert/benennt sie um. Gibt ein kurzes Protokoll zurück.
    public static string SyncObs(IInlineInvokeProxy cph, GrConfig cfg, GrWheel w)
    {
        return SyncObs(cph, cfg, w, true);
    }

    // refresh = false: Quelle nicht neu laden (Overlay unverändert), damit laufende Drehungen nicht abbrechen
    public static string SyncObs(IInlineInvokeProxy cph, GrConfig cfg, GrWheel w, bool refresh)
    {
        var log = new List<string>();
        string scene = ObsSceneName(w);
        string input = ObsInputName(w);
        var scenes = ObsScenes(cph, cfg);
        var inputs = ObsInputs(cph, cfg);

        // Umbenennen statt Duplikate erzeugen
        if (w.obs.syncedScene != "" && w.obs.syncedScene != scene && scenes.Contains(w.obs.syncedScene) && !scenes.Contains(scene))
        {
            ObsRequest(cph, cfg, "SetSceneName", D("sceneName", w.obs.syncedScene, "newSceneName", scene));
            log.Add("Szene umbenannt");
            scenes = ObsScenes(cph, cfg);
        }
        if (w.obs.syncedInput != "" && w.obs.syncedInput != input && inputs.Contains(w.obs.syncedInput) && !inputs.Contains(input))
        {
            ObsRequest(cph, cfg, "SetInputName", D("inputName", w.obs.syncedInput, "newInputName", input));
            log.Add("Quelle umbenannt");
            inputs = ObsInputs(cph, cfg);
        }

        if (!scenes.Contains(scene))
        {
            ObsRequest(cph, cfg, "CreateScene", D("sceneName", scene));
            log.Add("Szene angelegt");
        }

        var settings = D(
            "is_local_file", true,
            "local_file", OverlayPath(w).Replace('\\', '/'),
            "width", w.obs.width,
            "height", w.obs.height,
            "reroute_audio", true,
            "shutdown", false,
            "restart_when_active", false);

        if (!inputs.Contains(input))
        {
            var created = ObsRequest(cph, cfg, "CreateInput", D(
                "sceneName", scene, "inputName", input, "inputKind", "browser_source",
                "inputSettings", settings, "sceneItemEnabled", true));
            log.Add("Browser-Quelle angelegt");
            CenterItem(cph, cfg, scene, created, w);
        }
        else
        {
            ObsRequest(cph, cfg, "SetInputSettings", D("inputName", input, "inputSettings", settings, "overlay", true));
            if (refresh)
            {
                ObsRequest(cph, cfg, "PressInputPropertiesButton", D("inputName", input, "propertyName", "refreshnocache"));
                log.Add("Browser-Quelle aktualisiert");
            }
        }

        // Optional: Rad-Szene in eine andere Szene (z.B. "Live") einbetten
        if (w.obs.addToScene != "" && w.obs.addToScene != scene && scenes.Contains(w.obs.addToScene))
        {
            var items = NamesFrom(ObsRequest(cph, cfg, "GetSceneItemList", D("sceneName", w.obs.addToScene)), "sceneItems", "sourceName");
            if (!items.Contains(scene))
            {
                ObsRequest(cph, cfg, "CreateSceneItem", D("sceneName", w.obs.addToScene, "sourceName", scene, "sceneItemEnabled", true));
                log.Add("in Szene '" + w.obs.addToScene + "' eingefügt");
            }
        }

        w.obs.syncedScene = scene;
        w.obs.syncedInput = input;
        return string.Join(", ", log.ToArray());
    }

    static void CenterItem(IInlineInvokeProxy cph, GrConfig cfg, string scene, Dictionary<string, object> created, GrWheel w)
    {
        try
        {
            object idObj;
            if (!created.TryGetValue("sceneItemId", out idObj)) return;
            var video = ObsRequest(cph, cfg, "GetVideoSettings", null);
            double bw = 1920, bh = 1080;
            object v;
            if (video.TryGetValue("baseWidth", out v)) bw = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (video.TryGetValue("baseHeight", out v)) bh = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            var transform = D("positionX", Math.Max(0, (bw - w.obs.width) / 2), "positionY", Math.Max(0, (bh - w.obs.height) / 2));
            ObsRequest(cph, cfg, "SetSceneItemTransform", D("sceneName", scene, "sceneItemId", Convert.ToInt32(idObj), "sceneItemTransform", transform));
        }
        catch { /* Position ist nur Komfort */ }
    }

    public static void RemoveFromObs(IInlineInvokeProxy cph, GrConfig cfg, GrWheel w)
    {
        string input = w.obs.syncedInput != "" ? w.obs.syncedInput : ObsInputName(w);
        string scene = w.obs.syncedScene != "" ? w.obs.syncedScene : ObsSceneName(w);
        ObsRequest(cph, cfg, "RemoveInput", D("inputName", input));
        ObsRequest(cph, cfg, "RemoveScene", D("sceneName", scene));
    }
}
