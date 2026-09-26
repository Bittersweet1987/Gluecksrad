using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Streamer.bot.Plugin.Interface;
using System.Text;
using System.Web.Script.Serialization;

// Action "Glücksrad – Einstellungen"
// Öffnet das Einstellungsfenster. Hier werden Räder, Felder, Auslöser, Aussehen und OBS eingerichtet.










public class CPHInline
{
    // Wird von tools/build_import.py durch den Inhalt von overlay/overlay.html ersetzt.
    const string OverlayTemplate = @"<!DOCTYPE html>
<html lang=""de"">
<head>
<meta charset=""utf-8"">
<title>Glücksrad</title>
<style>
  html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: transparent; }
  #stage { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;
           opacity: 0; transition: opacity .6s ease; }
  #stage.visible { opacity: 1; }
  canvas { display: block; }
  #banner { position: absolute; left: 50%; bottom: 4%; transform: translateX(-50%) scale(.8);
            padding: .5em 1.2em; border-radius: .6em; background: rgba(0,0,0,.75); color: #fff;
            font: 700 clamp(18px, 4.2vmin, 48px)/1.2 ""Segoe UI"", Arial, sans-serif; text-align: center;
            max-width: 90%; opacity: 0; transition: opacity .4s ease, transform .4s ease; white-space: pre-line; }
  #banner.visible { opacity: 1; transform: translateX(-50%) scale(1); }
  #status { position: absolute; left: 8px; top: 8px; font: 12px/1.3 Consolas, monospace; color: #fff;
            background: rgba(180,0,0,.7); padding: 3px 6px; border-radius: 4px; display: none; }
</style>
</head>
<body>
<div id=""stage""><canvas id=""wheel""></canvas><div id=""banner""></div></div>
<div id=""status""></div>
<script>
// Von ""Glücksrad – Einstellungen"" beim Speichern ersetzt. Ohne Ersetzung gelten URL-Parameter.
const BOOT = /*GR_BOOT*/null/*GR_BOOT_END*/;

(function () {
  'use strict';

  const params = new URLSearchParams(location.search);
  const boot = BOOT || {};
  const WHEEL_ID = boot.wheelId || params.get('wheel') || '';
  const WS_HOST = boot.host || params.get('host') || '127.0.0.1';
  const WS_PORT = boot.port || params.get('port') || '8080';
  const RESULT_ACTION_ID = boot.resultActionId || 'a7f3c2e1-5b4d-4e8a-9c1f-2d3e4f5a6b72';
  const RESULT_ACTION_NAME = boot.resultActionName || 'Glücksrad – Ergebnis';
  const DEMO = params.get('demo') === '1';

  const canvas = document.getElementById('wheel');
  const ctx = canvas.getContext('2d');
  const stage = document.getElementById('stage');
  const banner = document.getElementById('banner');
  const statusEl = document.getElementById('status');

  const DEFAULT_LOOK = {
    spinSeconds: 12, spins: 8, borderColor: '#ffffff', borderWidth: 6, pointerColor: '#ffcc00',
    centerColor: '#ffffff', proportional: true, fontSize: 28, showBanner: true, holdSeconds: 6,
    idleVisible: false, tick: true, volume: 0.5
  };

  let wheel = boot.wheel || null;       // aktuelle Rad-Konfiguration
  let rotation = 0;                     // Grad, im Uhrzeigersinn
  let spinning = false;
  let highlight = -1;                   // Index des blinkenden Gewinnfelds
  let blinkOn = false;
  const queue = [];
  const imageCache = {};

  function look() { return Object.assign({}, DEFAULT_LOOK, (wheel && wheel.look) || {}); }

  // ---------- Geometrie ----------
  function segmentAngles(w) {
    const segs = (w && w.segments) || [];
    const prop = look().proportional;
    const weights = segs.map(s => prop ? Math.max(0, +s.weight || 0) : 1);
    let total = weights.reduce((a, b) => a + b, 0);
    if (total <= 0) { weights.fill(1); total = weights.length || 1; }
    let start = 0;
    return weights.map(wt => { const size = wt / total * 360; const r = { start, size }; start += size; return r; });
  }

  // ---------- Zeichnen ----------
  function resize() {
    const dim = Math.min(window.innerWidth, window.innerHeight);
    const dpr = window.devicePixelRatio || 1;
    canvas.style.width = canvas.style.height = dim + 'px';
    canvas.width = canvas.height = Math.round(dim * dpr);
    draw();
  }

  function getImage(src) {
    if (!src) return null;
    let img = imageCache[src];
    if (!img) { img = new Image(); img.onload = draw; img.src = src; imageCache[src] = img; }
    return img.complete && img.naturalWidth ? img : null;
  }

  function draw() {
    const W = canvas.width, c = W / 2;
    ctx.clearRect(0, 0, W, W);
    if (!wheel || !wheel.segments || !wheel.segments.length) return;
    const L = look();
    const scale = W / 1000;
    const R = 440 * scale;             // Radius des Rads
    const angles = segmentAngles(wheel);

    ctx.save();
    ctx.translate(c, c);
    ctx.rotate(rotation * Math.PI / 180);

    wheel.segments.forEach((seg, i) => {
      const a = angles[i];
      if (a.size <= 0) return;
      // 0° = oben (Zeiger), im Uhrzeigersinn
      const a0 = (a.start - 90) * Math.PI / 180;
      const a1 = (a.start + a.size - 90) * Math.PI / 180;
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.arc(0, 0, R, a0, a1);
      ctx.closePath();
      ctx.fillStyle = (i === highlight && blinkOn) ? '#ffffff' : (seg.color || '#888888');
      ctx.fill();
      ctx.lineWidth = Math.max(1, 2 * scale);
      ctx.strokeStyle = L.borderColor;
      ctx.stroke();

      const mid = (a.start + a.size / 2 - 90) * Math.PI / 180;
      ctx.save();
      ctx.rotate(mid);
      const img = getImage(seg.image);
      const hasText = seg.text && String(seg.text).trim() !== '';
      if (img) {
        // Bild zeigt mit der Oberkante nach außen, Größe an die Segmentbreite angepasst
        const arcW = 2 * R * 0.62 * Math.sin(Math.min(a.size, 170) / 2 * Math.PI / 180);
        const maxW = Math.min(arcW, R * 0.5);
        const ratio = img.naturalHeight / img.naturalWidth;
        let w = maxW, h = maxW * ratio;
        if (h > R * 0.45) { h = R * 0.45; w = h / ratio; }
        const dist = hasText ? R * 0.68 : R * 0.62;
        ctx.save();
        ctx.translate(dist, 0);
        ctx.rotate(Math.PI / 2);
        ctx.drawImage(img, -w / 2, -h / 2, w, h);
        ctx.restore();
      }
      if (hasText) {
        let fs = (+L.fontSize || 28) * scale;
        ctx.font = `700 ${fs}px ""Segoe UI"", Arial, sans-serif`;
        const maxLen = R * (img ? 0.42 : 0.72);
        while (ctx.measureText(seg.text).width > maxLen && fs > 10 * scale) {
          fs -= 1 * scale; ctx.font = `700 ${fs}px ""Segoe UI"", Arial, sans-serif`;
        }
        ctx.fillStyle = seg.textColor || '#000000';
        ctx.textAlign = 'right';
        ctx.textBaseline = 'middle';
        ctx.lineWidth = Math.max(2, fs / 7);
        ctx.strokeStyle = 'rgba(0,0,0,.35)';
        const tx = img ? R * 0.45 : R * 0.9;
        ctx.fillText(seg.text, tx, 0);
      }
      ctx.restore();
    });
    ctx.restore();

    // Außenring
    ctx.beginPath();
    ctx.arc(c, c, R, 0, Math.PI * 2);
    ctx.lineWidth = (+L.borderWidth || 6) * scale;
    ctx.strokeStyle = L.borderColor;
    ctx.stroke();

    // Nabe
    ctx.beginPath();
    ctx.arc(c, c, 50 * scale, 0, Math.PI * 2);
    ctx.fillStyle = L.centerColor;
    ctx.fill();
    ctx.lineWidth = 4 * scale;
    ctx.strokeStyle = L.borderColor;
    ctx.stroke();

    // Zeiger oben
    const tipY = c - R + 38 * scale;
    ctx.beginPath();
    ctx.moveTo(c, tipY);
    ctx.lineTo(c - 32 * scale, c - R - 32 * scale);
    ctx.lineTo(c + 32 * scale, c - R - 32 * scale);
    ctx.closePath();
    ctx.fillStyle = L.pointerColor;
    ctx.shadowColor = 'rgba(0,0,0,.5)';
    ctx.shadowBlur = 8 * scale;
    ctx.fill();
    ctx.shadowBlur = 0;
    ctx.lineWidth = 3 * scale;
    ctx.strokeStyle = 'rgba(0,0,0,.6)';
    ctx.stroke();
  }

  // ---------- Sound ----------
  let audioCtx = null;
  function tick() {
    const L = look();
    if (!L.tick) return;
    try {
      audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
      const t = audioCtx.currentTime;
      const osc = audioCtx.createOscillator();
      const gain = audioCtx.createGain();
      osc.type = 'square';
      osc.frequency.setValueAtTime(1400, t);
      osc.frequency.exponentialRampToValueAtTime(500, t + 0.03);
      gain.gain.setValueAtTime(0.15 * (+L.volume || 0), t);
      gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.04);
      osc.connect(gain).connect(audioCtx.destination);
      osc.start(t);
      osc.stop(t + 0.05);
    } catch (e) { /* kein Audio verfügbar */ }
  }

  function playSound(src) {
    if (!src) return;
    try { const a = new Audio(src); a.volume = Math.min(1, Math.max(0, +look().volume || 0.5)); a.play().catch(() => {}); }
    catch (e) { /* ignorieren */ }
  }

  // ---------- Ablauf ----------
  const sleep = ms => new Promise(r => setTimeout(r, ms));

  function segmentIndexAt(rot) {
    // Welches Segment liegt unter dem Zeiger (oben)?
    const pos = ((360 - (rot % 360)) % 360 + 360) % 360;
    const angles = segmentAngles(wheel);
    for (let i = 0; i < angles.length; i++) {
      if (pos >= angles[i].start && pos < angles[i].start + angles[i].size) return i;
    }
    return angles.length - 1;
  }

  function animateTo(target, seconds) {
    return new Promise(resolve => {
      const from = rotation;
      const dist = target - from;
      const dur = Math.max(1, seconds) * 1000;
      const t0 = performance.now();
      let lastSeg = segmentIndexAt(from);
      function frame(now) {
        const p = Math.min(1, (now - t0) / dur);
        const e = 1 - Math.pow(1 - p, 4);  // easeOutQuart
        rotation = from + dist * e;
        const s = segmentIndexAt(rotation);
        if (s !== lastSeg) { lastSeg = s; tick(); }
        draw();
        if (p < 1) requestAnimationFrame(frame); else resolve();
      }
      requestAnimationFrame(frame);
    });
  }

  async function runSpin(job) {
    spinning = true;
    wheel = job.wheel;
    highlight = -1;
    rotation = ((rotation % 360) + 360) % 360;
    draw();
    stage.classList.add('visible');
    await sleep(900);

    const L = look();
    const angles = segmentAngles(wheel);
    const idx = Math.min(Math.max(0, job.targetIndex | 0), angles.length - 1);
    const a = angles[idx];
    const pad = a.size * 0.12;
    const stopAt = a.start + pad + Math.random() * Math.max(0, a.size - 2 * pad);
    // Rotation, bei der stopAt unter dem Zeiger (0°) liegt
    const finalMod = (360 - stopAt) % 360;
    const base = rotation - (rotation % 360);
    let target = base + (+L.spins || 8) * 360 + finalMod;
    if (target - rotation < 720) target += 360;
    await animateTo(target, +L.spinSeconds || 12);

    const seg = wheel.segments[idx] || {};
    playSound(seg.sound);
    highlight = idx;
    const blink = setInterval(() => { blinkOn = !blinkOn; draw(); }, 300);
    if (L.showBanner) {
      const label = (seg.text && seg.text.trim()) || seg.name || ('Feld ' + (idx + 1));
      banner.textContent = (job.user ? job.user + ' gewinnt:\n' : 'Gewonnen:\n') + label;
      banner.classList.add('visible');
    }
    reportResult(job, idx);

    await sleep(Math.max(1, +L.holdSeconds || 6) * 1000);
    clearInterval(blink);
    blinkOn = false;
    highlight = -1;
    banner.classList.remove('visible');
    draw();
    if (!L.idleVisible) { stage.classList.remove('visible'); await sleep(700); }
    spinning = false;
    next();
  }

  function next() {
    if (spinning || !queue.length) return;
    runSpin(queue.shift());
  }

  function enqueue(job) {
    if (!job || !job.wheel || !job.wheel.segments || !job.wheel.segments.length) return;
    queue.push(job);
    next();
  }

  // ---------- Streamer.bot WebSocket ----------
  let ws = null;
  function setStatus(text) {
    statusEl.textContent = text || '';
    statusEl.style.display = text ? 'block' : 'none';
  }

  function connect() {
    try { ws = new WebSocket(`ws://${WS_HOST}:${WS_PORT}/`); }
    catch (e) { setStatus('Glücksrad: WebSocket-Fehler'); setTimeout(connect, 5000); return; }
    ws.onopen = () => {
      setStatus('');
      ws.send(JSON.stringify({ request: 'Subscribe', id: 'gr-sub', events: { General: ['Custom'] } }));
    };
    ws.onmessage = ev => handleMessage(ev.data);
    ws.onclose = () => {
      setStatus(`Glücksrad: keine Verbindung zu Streamer.bot (${WS_HOST}:${WS_PORT})`);
      setTimeout(connect, 3000);
    };
    ws.onerror = () => { try { ws.close(); } catch (e) { /* ignorieren */ } };
  }

  function handleMessage(raw) {
    let msg;
    try { msg = JSON.parse(raw); } catch (e) { return; }
    if (!msg || !msg.event || msg.event.source !== 'General' || msg.event.type !== 'Custom') return;
    let data = msg.data;
    if (typeof data === 'string') { try { data = JSON.parse(data); } catch (e) { return; } }
    if (!data || data.source !== 'gluecksrad') return;
    if (WHEEL_ID && data.wheelId !== WHEEL_ID) return;
    if (data.event === 'spin') enqueue(data);
    else if (data.event === 'preview' && !spinning) {
      wheel = data.wheel; draw();
      stage.classList.add('visible');
      setTimeout(() => { if (!spinning && !look().idleVisible) stage.classList.remove('visible'); }, 5000);
    }
  }
  window.grHandleMessage = handleMessage;   // für Tests

  function reportResult(job, idx) {
    if (DEMO || !job.spinId) return;
    if (!ws || ws.readyState !== 1) return;
    ws.send(JSON.stringify({
      request: 'DoAction', id: 'gr-result-' + job.spinId,
      action: { id: RESULT_ACTION_ID, name: RESULT_ACTION_NAME },
      args: { grWheelId: job.wheelId, grSpinId: job.spinId, grSegmentIndex: idx, grUser: job.user || '' }
    }));
  }

  // ---------- Start ----------
  window.addEventListener('resize', resize);
  resize();
  if (wheel && look().idleVisible) stage.classList.add('visible');

  if (DEMO) {
    const demoWheel = wheel || {
      id: 'demo', name: 'Demo',
      look: { spinSeconds: 6, spins: 5, idleVisible: true, holdSeconds: 3 },
      segments: [
        { text: 'Zonk', color: '#d50f25', textColor: '#ffffff', weight: 30 },
        { text: 'Freispin', color: '#ffff99', textColor: '#000000', weight: 15 },
        { text: '10 Pushups', color: '#009925', textColor: '#ffffff', weight: 20 },
        { text: '1000 Punkte', color: '#3369e8', textColor: '#ffffff', weight: 20 },
        { text: 'Jackpot', color: '#eeb211', textColor: '#000000', weight: 5 },
        { text: 'Nochmal Zonk', color: '#8e24aa', textColor: '#ffffff', weight: 10 }
      ]
    };
    wheel = demoWheel;
    stage.classList.add('visible');
    draw();
    window.grDemoSpin = (i, user) => enqueue({
      wheel: demoWheel, wheelId: 'demo', user: user || 'Demo',
      targetIndex: i != null ? i : Math.floor(Math.random() * demoWheel.segments.length)
    });
    document.addEventListener('click', () => window.grDemoSpin());
    window.grState = () => ({ rotation, underPointer: segmentIndexAt(rotation), spinning, queued: queue.length });
  } else {
    connect();
  }
})();
</script>
</body>
</html>
";

    static GrSettingsForm openForm;

    public bool Execute()
    {
        if (openForm != null && !openForm.IsDisposed)
        {
            try { openForm.Invoke((MethodInvoker)delegate { openForm.WindowState = FormWindowState.Normal; openForm.Activate(); }); }
            catch { }
            return true;
        }

        Exception error = null;
        var thread = new Thread(() =>
        {
            try
            {
                try { Application.EnableVisualStyles(); } catch { }
                using (var form = new GrSettingsForm(CPH, OverlayTemplate))
                {
                    openForm = form;
                    Application.Run(form);
                }
            }
            catch (Exception ex) { error = ex; }
            finally { openForm = null; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (error != null)
        {
            CPH.LogWarn("[Glücksrad] Fehler im Einstellungsfenster: " + error);
            return false;
        }
        return true;
    }
}

public class GrSettingsForm : Form
{
    readonly IInlineInvokeProxy cph;
    readonly string overlayTemplate;
    GrConfig cfg;
    GrWheel wheel;
    bool loading;
    bool dirty;
    List<string> actionNames = new List<string>();
    List<TwitchReward> rewards = new List<TwitchReward>();
    readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();

    // Steuerelemente
    ListBox wheelList;
    TabControl tabs;
    Panel preview;
    Label statusLabel;
    // Allgemein
    TextBox nameBox;
    CheckBox enabledBox;
    CheckBox chatAsBotBox;
    TextBox hostBox;
    NumericUpDown portBox;
    NumericUpDown obsConnBox;
    // Felder
    DataGridView grid;
    Label sumLabel;
    // Auslöser
    CheckedListBox rewardList;
    CheckBox bitsBox, subsBox, resubsBox, giftBox, tipsBox;
    NumericUpDown bitsMin, giftMin, tipMin;
    TextBox commandBox;
    // Aussehen
    NumericUpDown spinSeconds, spins, fontSize, borderWidth, holdSeconds, volume;
    Button borderColorBtn, pointerColorBtn, centerColorBtn;
    CheckBox proportionalBox, bannerBox, idleVisibleBox, tickBox;
    // OBS
    Label obsSceneLabel, obsInputLabel, obsStatusLabel;
    TextBox filePathBox;
    NumericUpDown obsWidth, obsHeight;
    ComboBox addToSceneBox;

    const int ColText = 0, ColColor = 1, ColTextColor = 2, ColImage = 3, ColWeight = 4, ColPercent = 5,
              ColChat = 6, ColAction = 7, ColSound = 8, ColFree = 9;

    public GrSettingsForm(IInlineInvokeProxy cph, string overlayTemplate)
    {
        this.cph = cph;
        this.overlayTemplate = overlayTemplate;
        cfg = GrCore.Load(cph);
        if (cfg.wheels.Count == 0) { cfg.wheels.Add(GrCore.ExampleWheel()); dirty = true; }

        LoadStreamerbotLists();
        DetectScale();
        BuildUi();
        FitToScreen();
        RefreshWheelList(0);
        UpdateStatusBar();
    }

    // Nur für Tests: erzwingt einen Skalierungsfaktor
    public static float ForcedScale = 0;

    // Streamer.bot ist DPI-aware: Schriften wachsen mit der Windows-Skalierung, Pixelmaße aber nicht.
    // Deshalb werden alle Pixelmaße beim Aufbau mit S() skaliert (z. B. Faktor 1,5 bei 150 %).
    static float uiScale = 1f;

    static int S(int v) { return (int)Math.Round(v * uiScale); }

    static void DetectScale()
    {
        float f = ForcedScale;
        if (f <= 0)
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) f = g.DpiX / 96f; }
            catch { f = 1f; }
        }
        uiScale = Math.Max(1f, f);
    }

    void FitToScreen()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width - 40), Math.Min(MinimumSize.Height, area.Height - 40));
        Size = new Size(Math.Min(Width, area.Width - 20), Math.Min(Height, area.Height - 20));
    }

    // ================= Daten aus Streamer.bot =================
    void LoadStreamerbotLists()
    {
        try
        {
            actionNames = cph.GetActions()
                .Where(a => a.Name != GrCore.SettingsActionName && a.Name != GrCore.SpinActionName && a.Name != GrCore.ResultActionName)
                .Select(a => a.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception ex) { cph.LogWarn("[Glücksrad] Actions konnten nicht geladen werden: " + ex.Message); }
        LoadRewards();
    }

    void LoadRewards()
    {
        try { rewards = cph.TwitchGetRewards() ?? new List<TwitchReward>(); }
        catch (Exception ex) { rewards = new List<TwitchReward>(); cph.LogWarn("[Glücksrad] Belohnungen konnten nicht geladen werden: " + ex.Message); }
    }

    string ResultActionId()
    {
        try
        {
            var a = cph.GetActions().FirstOrDefault(x => x.Name == GrCore.ResultActionName);
            if (a != null) return a.Id.ToString();
        }
        catch { }
        return GrCore.ResultActionId;
    }

    // ================= Aufbau der Oberfläche =================
    void BuildUi()
    {
        Text = "Glücksrad – Einstellungen";
        Font = new Font("Segoe UI", 9f);
        Size = new Size(S(1480), S(820));
        MinimumSize = new Size(S(1100), S(650));
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
        Shown += (s, e) => { TopMost = true; Activate(); TopMost = false; };

        tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(S(12), S(5)) };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildSegmentsTab());
        tabs.TabPages.Add(BuildTriggersTab());
        tabs.TabPages.Add(BuildLookTab());
        tabs.TabPages.Add(BuildObsTab());
        tabs.SelectedIndex = 1;

        // Rechte Seite: Vorschau
        var right = new Panel { Dock = DockStyle.Right, Width = S(330), Padding = new Padding(S(8)) };
        var previewTitle = new Label { Text = "Vorschau", Dock = DockStyle.Top, Height = S(24), Font = new Font(Font, FontStyle.Bold) };
        preview = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 40, 46) };
        preview.Paint += PaintPreview;
        preview.Resize += (s, e) => preview.Invalidate();
        right.Controls.Add(preview);
        right.Controls.Add(previewTitle);

        // Linke Seite: Liste der Räder
        var left = new Panel { Dock = DockStyle.Left, Width = S(220), Padding = new Padding(S(8)) };
        var leftTitle = new Label { Text = "Glücksräder", Dock = DockStyle.Top, Height = S(24), Font = new Font(Font, FontStyle.Bold) };
        wheelList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Segoe UI", 10f) };
        wheelList.SelectedIndexChanged += (s, e) => { if (!loading && wheelList.SelectedIndex >= 0) SelectWheel(wheelList.SelectedIndex); };
        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(70), FlowDirection = FlowDirection.LeftToRight };
        leftButtons.Controls.Add(MakeButton("Neu", S(96), (s, e) => AddWheel(false)));
        leftButtons.Controls.Add(MakeButton("Duplizieren", S(96), (s, e) => AddWheel(true)));
        leftButtons.Controls.Add(MakeButton("Löschen", S(96), (s, e) => DeleteWheel()));
        left.Controls.Add(wheelList);
        left.Controls.Add(leftButtons);
        left.Controls.Add(leftTitle);

        // Unten: Status und Knöpfe
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = S(52), Padding = new Padding(S(10), S(8), S(10), S(8)) };
        statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = S(440), FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(MakeButton("Schließen", S(120), (s, e) => Close()));
        var saveBtn = MakeButton("Speichern", S(120), (s, e) => SaveAll(true));
        saveBtn.Font = new Font(Font, FontStyle.Bold);
        buttons.Controls.Add(saveBtn);
        buttons.Controls.Add(MakeButton("Test-Drehen", S(120), (s, e) => TestSpin()));
        bottom.Controls.Add(statusLabel);
        bottom.Controls.Add(buttons);

        Controls.Add(tabs);
        Controls.Add(right);
        Controls.Add(left);
        Controls.Add(bottom);
    }

    TabPage BuildGeneralTab()
    {
        var page = new TabPage("Allgemein") { Padding = new Padding(S(12)), AutoScroll = true };
        var t = NewTable();
        nameBox = new TextBox { Width = S(320) };
        nameBox.TextChanged += (s, e) =>
        {
            if (loading) return;
            wheel.name = nameBox.Text.Trim();
            MarkDirty();
            UpdateWheelListText();
            UpdateObsLabels();
        };
        enabledBox = new CheckBox { Text = "Rad ist aktiv (reagiert auf Auslöser)", AutoSize = true };
        enabledBox.CheckedChanged += (s, e) => { if (loading) return; wheel.enabled = enabledBox.Checked; MarkDirty(); UpdateWheelListText(); };
        AddRow(t, "Name des Rads:", nameBox);
        AddRow(t, "", enabledBox);
        AddRow(t, "", Hint("Der Name bestimmt die OBS-Szene „Glücksrad – <Name>“ und die Browser-Quelle darin. " +
                           "Beim Speichern werden beide automatisch angelegt oder aktualisiert."));

        AddRow(t, "", Header("Für alle Räder"));
        chatAsBotBox = new CheckBox { Text = "Chatnachrichten mit dem Bot-Account senden", AutoSize = true };
        chatAsBotBox.CheckedChanged += (s, e) => { if (loading) return; cfg.chatAsBot = chatAsBotBox.Checked; MarkDirty(); };
        AddRow(t, "", chatAsBotBox);
        hostBox = new TextBox { Width = S(160) };
        hostBox.TextChanged += (s, e) => { if (loading) return; cfg.host = hostBox.Text.Trim(); MarkDirty(); };
        AddRow(t, "WebSocket-Server (Host):", hostBox);
        portBox = Num(1, 65535, 0);
        portBox.ValueChanged += (s, e) => { if (loading) return; cfg.port = (int)portBox.Value; MarkDirty(); };
        AddRow(t, "WebSocket-Server (Port):", portBox);
        obsConnBox = Num(0, 20, 0);
        obsConnBox.ValueChanged += (s, e) => { if (loading) return; cfg.obsConnection = (int)obsConnBox.Value; MarkDirty(); UpdateObsLabels(); };
        AddRow(t, "OBS-Verbindung (Nr.):", obsConnBox);
        AddRow(t, "", Hint("Der WebSocket-Server muss in Streamer.bot unter Servers/Clients → WebSocket Server laufen (Standard 127.0.0.1:8080). " +
                           "OBS muss unter Stream Apps → OBS verbunden sein. Die Nummer ist die Position in dieser Liste (erste = 0)."));
        page.Controls.Add(t);
        return page;
    }

    TabPage BuildSegmentsTab()
    {
        var page = new TabPage("Felder") { Padding = new Padding(S(8)) };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(38) };
        toolbar.Controls.Add(MakeButton("+ Feld", S(90), (s, e) => AddSegment()));
        toolbar.Controls.Add(MakeButton("Feld entfernen", S(110), (s, e) => RemoveSegment()));
        toolbar.Controls.Add(MakeButton("▲ Hoch", S(80), (s, e) => MoveSegment(-1)));
        toolbar.Controls.Add(MakeButton("▼ Runter", S(80), (s, e) => MoveSegment(1)));
        sumLabel = new Label { AutoSize = true, Padding = new Padding(S(10), S(8), S(0), S(0)) };
        toolbar.Controls.Add(sumLabel);

        grid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, BackgroundColor = SystemColors.Window,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        grid.RowTemplate.Height = S(28);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.Columns.Add(TextCol("Text auf dem Rad", S(130), false));
        grid.Columns.Add(TextCol("Farbe", S(64), true));
        grid.Columns.Add(TextCol("Textfarbe", S(64), true));
        grid.Columns.Add(TextCol("Bild", S(56), true));
        grid.Columns.Add(TextCol("Chance", S(56), false));
        grid.Columns.Add(TextCol("%", S(54), true));
        var chatCol = TextCol("Chatnachricht (%user%, %prize%)", S(260), false);
        chatCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        chatCol.MinimumWidth = S(180);
        grid.Columns.Add(chatCol);
        var actionCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Gewinn-Action", Width = S(150), FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
        };
        grid.Columns.Add(actionCol);
        grid.Columns.Add(TextCol("Sound", S(60), true));
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Freispin", Width = S(60) });
        foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns[ColPercent].DefaultCellStyle.ForeColor = Color.DimGray;
        grid.Columns[ColImage].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns[ColSound].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.CellValueChanged += OnGridValueChanged;
        grid.CellClick += OnGridCellClick;
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && (grid.CurrentCell is DataGridViewCheckBoxCell || grid.CurrentCell is DataGridViewComboBoxCell))
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.DataError += (s, e) => { e.ThrowException = false; };

        var hint = Hint("Farbe, Bild und Sound: Zelle anklicken. Chance = Gewichtung (muss nicht 100 ergeben). " +
                        "Gewinn-Action = optionale Streamer.bot-Action, die beim Gewinn läuft (bekommt %user% und %gluecksradPrize%).");
        hint.Dock = DockStyle.Bottom;
        hint.Padding = new Padding(S(0), S(6), S(0), S(0));
        page.Controls.Add(grid);
        page.Controls.Add(toolbar);
        page.Controls.Add(hint);
        return page;
    }

    TabPage BuildTriggersTab()
    {
        var page = new TabPage("Auslöser") { Padding = new Padding(S(12)), AutoScroll = true };
        var t = NewTable();

        AddRow(t, "", Hint("Wichtig: Die passenden Trigger müssen in Streamer.bot an die Action „Glücksrad – Drehen“ angehängt sein " +
                           "(Rechtsklick in Triggers → Twitch → Channel Reward → Reward Redemption, Twitch → Chat → Cheer, " +
                           "Twitch → Subscriptions → Subscription/Resubscription/Gift Subscription/Gift Bomb, Tipps unter Integrations, " +
                           "Befehle unter Core → Commands). Hier legst du fest, welches Rad sich bei welchem Ereignis dreht."));

        AddRow(t, "", Header("Kanalpunkte"));
        rewardList = new CheckedListBox { Width = S(420), Height = S(170), CheckOnClick = true, IntegralHeight = false };
        rewardList.ItemCheck += OnRewardCheck;
        AddRow(t, "Belohnungen:", rewardList);
        AddRow(t, "", MakeButton("Belohnungen neu laden", S(170), (s, e) => { LoadRewards(); FillRewards(); }));

        AddRow(t, "", Header("Bits, Subs und Spenden"));
        bitsBox = Check("Bits (Cheer) ab");
        bitsMin = Num(1, 1000000, 0);
        AddRow(t, "", Pair(bitsBox, bitsMin, "Bits"));
        subsBox = Check("Neue Subs");
        AddRow(t, "", subsBox);
        resubsBox = Check("Resubs");
        AddRow(t, "", resubsBox);
        giftBox = Check("Gift-Subs ab");
        giftMin = Num(1, 1000, 0);
        AddRow(t, "", Pair(giftBox, giftMin, "verschenkten Subs"));
        tipsBox = Check("Tipps / Spenden ab");
        tipMin = Num(0, 100000, 2);
        AddRow(t, "", Pair(tipsBox, tipMin, "(Betrag)"));
        AddRow(t, "", Hint("Eine Gift Bomb zählt als ein Ereignis mit der Anzahl der verschenkten Subs. " +
                           "Der Tipp-Betrag wird in der Währung deines Tipp-Dienstes verglichen. " +
                           "Passen mehrere Räder, dreht das Rad mit dem höchsten Mindestwert."));

        AddRow(t, "", Header("Chat-Befehl (z. B. für Mods zum Testen)"));
        commandBox = new TextBox { Width = S(160) };
        AddRow(t, "Befehl:", commandBox);
        AddRow(t, "", Hint("Den Befehl (z. B. !rad) zusätzlich in Streamer.bot unter Commands anlegen und als Trigger an „Glücksrad – Drehen“ hängen."));

        EventHandler changed = (s, e) =>
        {
            if (loading) return;
            var tr = wheel.triggers;
            tr.bits = bitsBox.Checked; tr.bitsMin = (int)bitsMin.Value;
            tr.subs = subsBox.Checked; tr.resubs = resubsBox.Checked;
            tr.giftSubs = giftBox.Checked; tr.giftMin = (int)giftMin.Value;
            tr.tips = tipsBox.Checked; tr.tipMin = (double)tipMin.Value;
            tr.command = commandBox.Text.Trim();
            MarkDirty();
        };
        foreach (var c in new Control[] { bitsBox, subsBox, resubsBox, giftBox, tipsBox })
            ((CheckBox)c).CheckedChanged += changed;
        bitsMin.ValueChanged += changed; giftMin.ValueChanged += changed; tipMin.ValueChanged += changed;
        commandBox.TextChanged += changed;

        page.Controls.Add(t);
        return page;
    }

    TabPage BuildLookTab()
    {
        var page = new TabPage("Aussehen") { Padding = new Padding(S(12)), AutoScroll = true };
        var t = NewTable();
        spinSeconds = Num(2, 120, 1);
        spins = Num(1, 50, 0);
        holdSeconds = Num(1, 60, 1);
        fontSize = Num(8, 120, 0);
        borderWidth = Num(0, 40, 0);
        volume = Num(0, 100, 0);
        borderColorBtn = ColorButton();
        pointerColorBtn = ColorButton();
        centerColorBtn = ColorButton();
        proportionalBox = Check("Feldgröße entspricht der Chance (sonst alle Felder gleich groß)");
        bannerBox = Check("Gewinner-Einblendung unter dem Rad anzeigen");
        idleVisibleBox = Check("Rad auch ohne Drehung dauerhaft anzeigen");
        tickBox = Check("Tick-Geräusch beim Drehen");

        AddRow(t, "Drehdauer (Sekunden):", spinSeconds);
        AddRow(t, "Umdrehungen:", spins);
        AddRow(t, "Anzeige nach Gewinn (Sek.):", holdSeconds);
        AddRow(t, "Schriftgröße:", fontSize);
        AddRow(t, "Randbreite:", borderWidth);
        AddRow(t, "Randfarbe:", borderColorBtn);
        AddRow(t, "Zeigerfarbe:", pointerColorBtn);
        AddRow(t, "Farbe der Mitte:", centerColorBtn);
        AddRow(t, "Lautstärke (%):", volume);
        AddRow(t, "", proportionalBox);
        AddRow(t, "", bannerBox);
        AddRow(t, "", idleVisibleBox);
        AddRow(t, "", tickBox);

        EventHandler changed = (s, e) =>
        {
            if (loading) return;
            var l = wheel.look;
            l.spinSeconds = (double)spinSeconds.Value; l.spins = (int)spins.Value; l.holdSeconds = (double)holdSeconds.Value;
            l.fontSize = (int)fontSize.Value; l.borderWidth = (int)borderWidth.Value; l.volume = (double)volume.Value / 100.0;
            l.borderColor = ToHex(borderColorBtn.BackColor); l.pointerColor = ToHex(pointerColorBtn.BackColor);
            l.centerColor = ToHex(centerColorBtn.BackColor);
            l.proportional = proportionalBox.Checked; l.showBanner = bannerBox.Checked;
            l.idleVisible = idleVisibleBox.Checked; l.tick = tickBox.Checked;
            MarkDirty();
            UpdatePercentages();
            preview.Invalidate();
        };
        foreach (var n in new[] { spinSeconds, spins, holdSeconds, fontSize, borderWidth, volume }) n.ValueChanged += changed;
        foreach (var b in new[] { borderColorBtn, pointerColorBtn, centerColorBtn }) b.BackColorChanged += changed;
        foreach (var c in new[] { proportionalBox, bannerBox, idleVisibleBox, tickBox }) c.CheckedChanged += changed;

        page.Controls.Add(t);
        return page;
    }

    TabPage BuildObsTab()
    {
        var page = new TabPage("OBS") { Padding = new Padding(S(12)), AutoScroll = true };
        var t = NewTable();
        AddRow(t, "", Hint("Beim Speichern legt das Glücksrad in OBS automatisch eine eigene Szene mit einer Browser-Quelle an " +
                           "und aktualisiert sie bei jeder Änderung. Benennst du das Rad um, wird auch in OBS umbenannt."));
        obsStatusLabel = new Label { AutoSize = true };
        AddRow(t, "OBS-Status:", obsStatusLabel);
        obsSceneLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        obsInputLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        AddRow(t, "Szene:", obsSceneLabel);
        AddRow(t, "Browser-Quelle:", obsInputLabel);
        obsWidth = Num(200, 4000, 0);
        obsHeight = Num(200, 4000, 0);
        AddRow(t, "Breite der Quelle:", obsWidth);
        AddRow(t, "Höhe der Quelle:", obsHeight);
        addToSceneBox = new ComboBox { Width = S(320), DropDownStyle = ComboBoxStyle.DropDownList };
        AddRow(t, "Zusätzlich einfügen in:", addToSceneBox);
        AddRow(t, "", Hint("Wähle z. B. deine Live-Szene: Die Rad-Szene wird dort einmalig als Quelle eingefügt, " +
                           "damit das Rad im Stream erscheint. Das Rad ist nur während einer Drehung sichtbar."));
        AddRow(t, "", MakeButton("Szenenliste neu laden", S(170), (s, e) => FillScenes()));
        filePathBox = new TextBox { Width = S(520), ReadOnly = true };
        AddRow(t, "Overlay-Datei:", filePathBox);
        var fileButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        fileButtons.Controls.Add(MakeButton("Pfad kopieren", S(130), (s, e) => { try { Clipboard.SetText(filePathBox.Text); SetStatus("Pfad kopiert."); } catch { } }));
        fileButtons.Controls.Add(MakeButton("Ordner öffnen", S(130), (s, e) =>
        {
            try { Directory.CreateDirectory(GrCore.OverlayDir()); System.Diagnostics.Process.Start("explorer.exe", GrCore.OverlayDir()); } catch { }
        }));
        fileButtons.Controls.Add(MakeButton("Jetzt mit OBS synchronisieren", S(220), (s, e) => SaveAll(true)));
        AddRow(t, "", fileButtons);

        EventHandler changed = (s, e) =>
        {
            if (loading) return;
            wheel.obs.width = (int)obsWidth.Value;
            wheel.obs.height = (int)obsHeight.Value;
            wheel.obs.addToScene = addToSceneBox.SelectedIndex > 0 ? addToSceneBox.SelectedItem.ToString() : "";
            MarkDirty();
        };
        obsWidth.ValueChanged += changed; obsHeight.ValueChanged += changed; addToSceneBox.SelectedIndexChanged += changed;
        page.Controls.Add(t);
        return page;
    }

    // ================= Räder =================
    void RefreshWheelList(int select)
    {
        loading = true;
        wheelList.Items.Clear();
        foreach (var w in cfg.wheels) wheelList.Items.Add(WheelListText(w));
        loading = false;
        if (cfg.wheels.Count > 0)
        {
            select = Math.Max(0, Math.Min(select, cfg.wheels.Count - 1));
            wheelList.SelectedIndex = select;
            SelectWheel(select);
        }
        else
        {
            wheel = null;
            tabs.Enabled = false;
            preview.Invalidate();
        }
    }

    string WheelListText(GrWheel w)
    {
        return (string.IsNullOrEmpty(w.name) ? "(ohne Name)" : w.name) + (w.enabled ? "" : "  (aus)");
    }

    void UpdateWheelListText()
    {
        int i = cfg.wheels.IndexOf(wheel);
        if (i < 0 || i >= wheelList.Items.Count) return;
        bool l = loading;
        loading = true;
        wheelList.Items[i] = WheelListText(wheel);
        loading = l;
    }

    void SelectWheel(int index)
    {
        if (grid.IsCurrentCellInEditMode) grid.EndEdit();
        wheel = cfg.wheels[index];
        tabs.Enabled = true;
        loading = true;
        nameBox.Text = wheel.name;
        enabledBox.Checked = wheel.enabled;
        chatAsBotBox.Checked = cfg.chatAsBot;
        hostBox.Text = cfg.host;
        portBox.Value = Clamp(cfg.port, portBox);
        obsConnBox.Value = Clamp(cfg.obsConnection, obsConnBox);

        var tr = wheel.triggers;
        bitsBox.Checked = tr.bits; bitsMin.Value = Clamp(tr.bitsMin, bitsMin);
        subsBox.Checked = tr.subs; resubsBox.Checked = tr.resubs;
        giftBox.Checked = tr.giftSubs; giftMin.Value = Clamp(tr.giftMin, giftMin);
        tipsBox.Checked = tr.tips; tipMin.Value = Clamp((decimal)tr.tipMin, tipMin);
        commandBox.Text = tr.command;
        FillRewards();

        var l = wheel.look;
        spinSeconds.Value = Clamp((decimal)l.spinSeconds, spinSeconds); spins.Value = Clamp(l.spins, spins);
        holdSeconds.Value = Clamp((decimal)l.holdSeconds, holdSeconds); fontSize.Value = Clamp(l.fontSize, fontSize);
        borderWidth.Value = Clamp(l.borderWidth, borderWidth); volume.Value = Clamp((decimal)(l.volume * 100), volume);
        borderColorBtn.BackColor = FromHex(l.borderColor); pointerColorBtn.BackColor = FromHex(l.pointerColor);
        centerColorBtn.BackColor = FromHex(l.centerColor);
        proportionalBox.Checked = l.proportional; bannerBox.Checked = l.showBanner;
        idleVisibleBox.Checked = l.idleVisible; tickBox.Checked = l.tick;

        obsWidth.Value = Clamp(wheel.obs.width, obsWidth);
        obsHeight.Value = Clamp(wheel.obs.height, obsHeight);
        FillScenes();

        FillGrid();
        loading = false;
        UpdateObsLabels();
        preview.Invalidate();
    }

    void AddWheel(bool duplicate)
    {
        GrWheel w;
        if (duplicate && wheel != null)
        {
            w = GrCore.Json().Deserialize<GrWheel>(GrCore.Json().Serialize(wheel));
            w.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            w.name = UniqueName(wheel.name + " Kopie");
            w.obs.syncedScene = ""; w.obs.syncedInput = "";
            w.triggers.rewardIds = new List<string>();
        }
        else
        {
            w = GrCore.ExampleWheel();
            w.name = UniqueName("Neues Rad");
        }
        cfg.wheels.Add(w);
        GrCore.Normalize(cfg);
        MarkDirty();
        RefreshWheelList(cfg.wheels.Count - 1);
        tabs.SelectedIndex = 0;
        nameBox.Focus();
        nameBox.SelectAll();
    }

    string UniqueName(string baseName)
    {
        string n = baseName;
        int i = 2;
        while (cfg.wheels.Any(x => string.Equals(x.name, n, StringComparison.OrdinalIgnoreCase))) n = baseName + " " + i++;
        return n;
    }

    void DeleteWheel()
    {
        if (wheel == null) return;
        if (MessageBox.Show(this, "Rad „" + wheel.name + "“ wirklich löschen?", "Rad löschen",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        if (wheel.obs.syncedScene != "" && ObsConnected() &&
            MessageBox.Show(this, "Auch die Szene „" + wheel.obs.syncedScene + "“ und die Browser-Quelle in OBS löschen?",
                "OBS aufräumen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try { GrCore.RemoveFromObs(cph, cfg, wheel); } catch (Exception ex) { SetStatus("OBS: " + ex.Message); }
        }
        int i = cfg.wheels.IndexOf(wheel);
        cfg.wheels.Remove(wheel);
        MarkDirty();
        RefreshWheelList(i);
    }

    // ================= Felder (Tabelle) =================
    void FillGrid()
    {
        bool l = loading;
        loading = true;
        var actionCol = (DataGridViewComboBoxColumn)grid.Columns[ColAction];
        var items = new List<string> { "" };
        items.AddRange(actionNames);
        foreach (var s in wheel.segments)
            if (s.action != "" && !items.Contains(s.action)) items.Add(s.action);
        actionCol.Items.Clear();
        actionCol.Items.AddRange(items.ToArray());

        grid.Rows.Clear();
        foreach (var s in wheel.segments)
        {
            int r = grid.Rows.Add();
            FillRow(grid.Rows[r], s);
        }
        loading = l;
        UpdatePercentages();
    }

    void FillRow(DataGridViewRow row, GrSegment s)
    {
        row.Cells[ColText].Value = s.text;
        SetColorCell(row.Cells[ColColor], s.color);
        SetColorCell(row.Cells[ColTextColor], s.textColor);
        row.Cells[ColImage].Value = s.image != "" ? "✔ Bild" : "–";
        row.Cells[ColWeight].Value = s.weight.ToString("0.##", CultureInfo.CurrentCulture);
        row.Cells[ColChat].Value = s.chat;
        row.Cells[ColAction].Value = s.action;
        row.Cells[ColSound].Value = s.sound != "" ? "✔ Sound" : "–";
        row.Cells[ColFree].Value = s.freeSpin;
    }

    void SetColorCell(DataGridViewCell cell, string hex)
    {
        var c = FromHex(hex);
        cell.Value = hex;
        cell.Style.BackColor = c;
        cell.Style.SelectionBackColor = c;
        var fg = (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 140 ? Color.Black : Color.White;
        cell.Style.ForeColor = fg;
        cell.Style.SelectionForeColor = fg;
    }

    void OnGridValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (loading || wheel == null || e.RowIndex < 0 || e.RowIndex >= wheel.segments.Count) return;
        var s = wheel.segments[e.RowIndex];
        var v = grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
        string str = v == null ? "" : v.ToString();
        switch (e.ColumnIndex)
        {
            case ColText: s.text = str; break;
            case ColChat: s.chat = str; break;
            case ColAction: s.action = str; break;
            case ColFree: s.freeSpin = v is bool && (bool)v; break;
            case ColWeight:
                double d;
                if (GrCore.TryParseNumber(str, out d) && d >= 0) s.weight = d;
                else
                {
                    loading = true;
                    grid.Rows[e.RowIndex].Cells[ColWeight].Value = s.weight.ToString("0.##", CultureInfo.CurrentCulture);
                    loading = false;
                }
                UpdatePercentages();
                break;
            default: return;
        }
        MarkDirty();
        preview.Invalidate();
    }

    void OnGridCellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (wheel == null || e.RowIndex < 0 || e.RowIndex >= wheel.segments.Count) return;
        var s = wheel.segments[e.RowIndex];
        var row = grid.Rows[e.RowIndex];
        if (e.ColumnIndex == ColColor || e.ColumnIndex == ColTextColor)
        {
            bool isText = e.ColumnIndex == ColTextColor;
            using (var dlg = new ColorDialog { FullOpen = true, Color = FromHex(isText ? s.textColor : s.color) })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string hex = ToHex(dlg.Color);
                if (isText) s.textColor = hex; else s.color = hex;
                loading = true;
                SetColorCell(row.Cells[e.ColumnIndex], hex);
                loading = false;
                MarkDirty();
                preview.Invalidate();
            }
        }
        else if (e.ColumnIndex == ColImage || e.ColumnIndex == ColSound)
        {
            bool isImage = e.ColumnIndex == ColImage;
            var menu = new ContextMenuStrip();
            menu.Items.Add(isImage ? "Bild auswählen …" : "Sound auswählen …", null, (o, a) =>
            {
                string uri = PickFileAsDataUri(isImage);
                if (uri == null) return;
                if (isImage) s.image = uri; else s.sound = uri;
                AfterMediaChange(row, s);
            });
            var remove = menu.Items.Add(isImage ? "Bild entfernen" : "Sound entfernen", null, (o, a) =>
            {
                if (isImage) s.image = ""; else s.sound = "";
                AfterMediaChange(row, s);
            });
            remove.Enabled = isImage ? s.image != "" : s.sound != "";
            if (!isImage)
            {
                var play = menu.Items.Add("Anhören", null, (o, a) => PlayDataUri(s.sound));
                play.Enabled = s.sound != "";
            }
            menu.Show(Cursor.Position);
        }
    }

    void AfterMediaChange(DataGridViewRow row, GrSegment s)
    {
        loading = true;
        FillRow(row, s);
        loading = false;
        MarkDirty();
        preview.Invalidate();
    }

    void AddSegment()
    {
        if (wheel == null) return;
        var s = new GrSegment();
        var palette = new[] { "#d50f25", "#3369e8", "#009925", "#eeb211", "#8e24aa", "#ff6d00", "#00acc1", "#6d4c41" };
        s.color = palette[wheel.segments.Count % palette.Length];
        s.textColor = s.color == "#eeb211" ? "#000000" : "#ffffff";
        s.text = "Feld " + (wheel.segments.Count + 1);
        wheel.segments.Add(s);
        FillGrid();
        grid.CurrentCell = grid.Rows[grid.Rows.Count - 1].Cells[ColText];
        MarkDirty();
        preview.Invalidate();
    }

    void RemoveSegment()
    {
        if (wheel == null || grid.CurrentCell == null) return;
        int i = grid.CurrentCell.RowIndex;
        if (i < 0 || i >= wheel.segments.Count) return;
        wheel.segments.RemoveAt(i);
        FillGrid();
        if (grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[Math.Min(i, grid.Rows.Count - 1)].Cells[ColText];
        MarkDirty();
        preview.Invalidate();
    }

    void MoveSegment(int dir)
    {
        if (wheel == null || grid.CurrentCell == null) return;
        int i = grid.CurrentCell.RowIndex, j = i + dir, col = grid.CurrentCell.ColumnIndex;
        if (i < 0 || j < 0 || j >= wheel.segments.Count) return;
        var tmp = wheel.segments[i];
        wheel.segments[i] = wheel.segments[j];
        wheel.segments[j] = tmp;
        FillGrid();
        grid.CurrentCell = grid.Rows[j].Cells[col];
        MarkDirty();
        preview.Invalidate();
    }

    void UpdatePercentages()
    {
        if (wheel == null) return;
        double total = wheel.segments.Sum(x => Math.Max(0, x.weight));
        bool l = loading;
        loading = true;
        for (int i = 0; i < wheel.segments.Count && i < grid.Rows.Count; i++)
        {
            double p = total > 0 ? Math.Max(0, wheel.segments[i].weight) / total * 100 : 100.0 / wheel.segments.Count;
            grid.Rows[i].Cells[ColPercent].Value = p.ToString("0.0", CultureInfo.CurrentCulture) + " %";
        }
        loading = l;
        sumLabel.Text = wheel.segments.Count + " Felder · Summe der Chancen: " + total.ToString("0.##", CultureInfo.CurrentCulture);
    }

    // ================= Auslöser =================
    void FillRewards()
    {
        bool l = loading;
        loading = true;
        rewardList.Items.Clear();
        foreach (var r in rewards.OrderBy(x => x.Title))
            rewardList.Items.Add(new RewardItem(r), wheel != null && wheel.triggers.rewardIds.Contains(r.Id));
        if (wheel != null)
            foreach (var id in wheel.triggers.rewardIds.Where(id => !rewards.Any(r => r.Id == id)))
                rewardList.Items.Add(new RewardItem(id), true);
        if (rewardList.Items.Count == 0) rewardList.Items.Add("(keine Belohnungen gefunden – ist Twitch verbunden?)");
        loading = l;
    }

    void OnRewardCheck(object sender, ItemCheckEventArgs e)
    {
        if (loading || wheel == null) return;
        var item = rewardList.Items[e.Index] as RewardItem;
        if (item == null) { e.NewValue = CheckState.Unchecked; return; }
        var ids = wheel.triggers.rewardIds;
        if (e.NewValue == CheckState.Checked)
        {
            if (!ids.Contains(item.Id)) ids.Add(item.Id);
            // Eine Belohnung gehört immer nur zu einem Rad
            foreach (var other in cfg.wheels.Where(w => w != wheel)) other.triggers.rewardIds.Remove(item.Id);
        }
        else ids.Remove(item.Id);
        MarkDirty();
    }

    class RewardItem
    {
        public string Id;
        string label;
        public RewardItem(TwitchReward r) { Id = r.Id; label = r.Title + "  (" + r.Cost + " Punkte)" + (r.Enabled ? "" : "  [deaktiviert]"); }
        public RewardItem(string id) { Id = id; label = "Unbekannte Belohnung " + id; }
        public override string ToString() { return label; }
    }

    // ================= OBS =================
    bool ObsConnected()
    {
        try { return cph.ObsIsConnected(cfg.obsConnection); } catch { return false; }
    }

    void FillScenes()
    {
        bool l = loading;
        loading = true;
        addToSceneBox.Items.Clear();
        addToSceneBox.Items.Add("(nicht einfügen)");
        var scenes = new List<string>();
        if (ObsConnected())
        {
            try { scenes = GrCore.ObsScenes(cph, cfg).Where(s => !s.StartsWith("Glücksrad – ")).ToList(); } catch { }
        }
        foreach (var s in scenes) addToSceneBox.Items.Add(s);
        string sel = wheel != null ? wheel.obs.addToScene : "";
        if (sel != "" && !scenes.Contains(sel)) addToSceneBox.Items.Add(sel);
        addToSceneBox.SelectedIndex = sel != "" ? addToSceneBox.Items.IndexOf(sel) : 0;
        loading = l;
    }

    void UpdateObsLabels()
    {
        if (wheel == null) return;
        bool connected = ObsConnected();
        obsStatusLabel.Text = connected ? "verbunden ✔" : "nicht verbunden – Szene/Quelle werden beim nächsten Speichern mit verbundenem OBS angelegt";
        obsStatusLabel.ForeColor = connected ? Color.ForestGreen : Color.Firebrick;
        obsSceneLabel.Text = GrCore.ObsSceneName(wheel);
        obsInputLabel.Text = GrCore.ObsInputName(wheel);
        filePathBox.Text = GrCore.OverlayPath(wheel);
    }

    // ================= Speichern / Testen =================
    bool ValidateConfig(out string error)
    {
        error = null;
        foreach (var w in cfg.wheels)
        {
            if (string.IsNullOrWhiteSpace(w.name)) { error = "Jedes Rad braucht einen Namen."; return false; }
            if (cfg.wheels.Count(x => string.Equals(x.name.Trim(), w.name.Trim(), StringComparison.OrdinalIgnoreCase)) > 1)
            { error = "Der Name „" + w.name + "“ ist doppelt vergeben."; return false; }
            if (w.name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
            { error = "Der Name „" + w.name + "“ enthält unerlaubte Zeichen (/ \\ : * ? \" < > |)."; return false; }
            if (w.segments.Count < 2) { error = "Das Rad „" + w.name + "“ braucht mindestens 2 Felder."; return false; }
        }
        return true;
    }

    bool SaveAll(bool syncObs)
    {
        if (grid.IsCurrentCellInEditMode) grid.EndEdit();
        string error;
        if (!ValidateConfig(out error))
        {
            MessageBox.Show(this, error, "Speichern nicht möglich", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        foreach (var w in cfg.wheels) w.name = w.name.Trim();
        Cursor = Cursors.WaitCursor;
        var report = new List<string>();
        try
        {
            // Overlay-Dateien schreiben (eine pro Rad) und alte entfernen
            string dir = GrCore.OverlayDir();
            Directory.CreateDirectory(dir);
            string resultId = ResultActionId();
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in cfg.wheels)
            {
                string path = GrCore.OverlayPath(w);
                File.WriteAllText(path, GrCore.BuildOverlayHtml(overlayTemplate, cfg, w, resultId), new System.Text.UTF8Encoding(false));
                keep.Add(Path.GetFileName(path));
            }
            foreach (var f in Directory.GetFiles(dir, "rad_*.html"))
                if (!keep.Contains(Path.GetFileName(f))) { try { File.Delete(f); } catch { } }

            // OBS synchronisieren
            if (syncObs)
            {
                if (ObsConnected())
                {
                    foreach (var w in cfg.wheels)
                    {
                        try
                        {
                            string r = GrCore.SyncObs(cph, cfg, w);
                            if (r != "") report.Add(w.name + ": " + r);
                        }
                        catch (Exception ex) { report.Add(w.name + ": OBS-Fehler " + ex.Message); }
                    }
                }
                else report.Add("OBS nicht verbunden – Szenen werden beim nächsten Speichern angelegt");
            }

            GrCore.Save(cph, cfg);
            dirty = false;
            UpdateStatusBar();
            UpdateObsLabels();
            FillScenes();
            SetStatus("Gespeichert " + DateTime.Now.ToString("HH:mm:ss") + (report.Count > 0 ? " · " + string.Join(" · ", report.ToArray()) : ""));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Fehler beim Speichern: " + ex.Message, "Glücksrad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally { Cursor = Cursors.Default; }
    }

    void TestSpin()
    {
        if (wheel == null) return;
        if (dirty && !SaveAll(true)) return;
        GrCore.StartSpin(cph, wheel, "Test", true);
        SetStatus("Test-Drehung gestartet für „" + wheel.name + "“. Die Szene muss in OBS sichtbar sein. " +
                  "Bei Test-Drehungen werden keine Gewinn-Actions ausgeführt.");
    }

    void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (!dirty) return;
        var r = MessageBox.Show(this, "Es gibt ungespeicherte Änderungen. Jetzt speichern?", "Glücksrad",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Cancel) e.Cancel = true;
        else if (r == DialogResult.Yes && !SaveAll(true)) e.Cancel = true;
    }

    void MarkDirty()
    {
        if (loading) return;
        dirty = true;
        UpdateStatusBar();
    }

    void UpdateStatusBar()
    {
        Text = "Glücksrad – Einstellungen" + (dirty ? "  •  ungespeichert" : "");
    }

    void SetStatus(string text) { statusLabel.Text = text; }

    // ================= Vorschau =================
    void PaintPreview(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        if (wheel == null || wheel.segments.Count == 0) return;

        float size = Math.Min(preview.Width, preview.Height) - 40;
        if (size < 50) return;
        float cx = preview.Width / 2f, cy = preview.Height / 2f, r = size / 2f;
        var rect = new RectangleF(cx - r, cy - r, size, size);
        var angles = SegmentAngles();
        var look = wheel.look;
        float scale = size / 880f;

        for (int i = 0; i < wheel.segments.Count; i++)
        {
            var s = wheel.segments[i];
            float start = (float)angles[i][0] - 90f, sweep = (float)angles[i][1];
            if (sweep <= 0) continue;
            using (var b = new SolidBrush(FromHex(s.color))) g.FillPie(b, rect.X, rect.Y, rect.Width, rect.Height, start, sweep);
            using (var p = new Pen(FromHex(look.borderColor), Math.Max(1f, 2 * scale))) g.DrawPie(p, rect.X, rect.Y, rect.Width, rect.Height, start, sweep);

            float mid = start + sweep / 2f;
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(mid);
            var img = GetImage(s.image);
            bool hasText = !string.IsNullOrWhiteSpace(s.text);
            if (img != null)
            {
                double arcW = 2 * r * 0.62 * Math.Sin(Math.Min(sweep, 170) / 2 * Math.PI / 180);
                float w = (float)Math.Min(arcW, r * 0.5);
                float h = w * img.Height / Math.Max(1, img.Width);
                if (h > r * 0.45f) { h = r * 0.45f; w = h * img.Width / Math.Max(1, img.Height); }
                float dist = hasText ? r * 0.68f : r * 0.62f;
                var st2 = g.Save();
                g.TranslateTransform(dist, 0);
                g.RotateTransform(90);
                g.DrawImage(img, -w / 2, -h / 2, w, h);
                g.Restore(st2);
            }
            if (hasText)
            {
                float fs = Math.Max(6f, look.fontSize * scale);
                float maxLen = r * (img != null ? 0.42f : 0.72f);
                Font f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
                while (g.MeasureString(s.text, f).Width > maxLen && fs > 6f)
                {
                    f.Dispose();
                    fs -= 0.5f;
                    f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
                }
                var sz = g.MeasureString(s.text, f);
                float tx = img != null ? r * 0.45f : r * 0.9f;
                using (var b = new SolidBrush(FromHex(s.textColor))) g.DrawString(s.text, f, b, tx - sz.Width, -sz.Height / 2);
                f.Dispose();
            }
            g.Restore(state);
        }
        using (var p = new Pen(FromHex(look.borderColor), Math.Max(1f, look.borderWidth * scale))) g.DrawEllipse(p, rect);
        float hub = 50 * scale;
        using (var b = new SolidBrush(FromHex(look.centerColor))) g.FillEllipse(b, cx - hub, cy - hub, hub * 2, hub * 2);
        var tri = new[] { new PointF(cx, cy - r + 38 * scale), new PointF(cx - 32 * scale, cy - r - 32 * scale), new PointF(cx + 32 * scale, cy - r - 32 * scale) };
        using (var b = new SolidBrush(FromHex(look.pointerColor))) g.FillPolygon(b, tri);
        using (var p = new Pen(Color.FromArgb(150, 0, 0, 0), 2)) g.DrawPolygon(p, tri);
    }

    List<double[]> SegmentAngles()
    {
        var segs = wheel.segments;
        var weights = segs.Select(s => wheel.look.proportional ? Math.Max(0, s.weight) : 1.0).ToList();
        double total = weights.Sum();
        if (total <= 0) { weights = segs.Select(s => 1.0).ToList(); total = weights.Count; }
        var result = new List<double[]>();
        double start = 0;
        foreach (var w in weights) { double sw = w / total * 360; result.Add(new[] { start, sw }); start += sw; }
        return result;
    }

    Image GetImage(string dataUri)
    {
        if (string.IsNullOrEmpty(dataUri)) return null;
        Image img;
        if (imageCache.TryGetValue(dataUri, out img)) return img;
        img = null;
        try
        {
            int comma = dataUri.IndexOf(',');
            var bytes = Convert.FromBase64String(dataUri.Substring(comma + 1));
            using (var ms = new MemoryStream(bytes))
            using (var tmp = Image.FromStream(ms)) img = new Bitmap(tmp);
        }
        catch { img = null; }   // z.B. WebP/SVG: kann GDI+ nicht anzeigen, im Overlay funktioniert es trotzdem
        imageCache[dataUri] = img;
        return img;
    }

    // ================= Dateien =================
    string PickFileAsDataUri(bool image)
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = image ? "Bild für das Feld auswählen" : "Sound für das Feld auswählen";
            dlg.Filter = image ? "Bilder|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.svg" : "Sounds|*.mp3;*.wav;*.ogg";
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            var info = new FileInfo(dlg.FileName);
            if (info.Length > 5 * 1024 * 1024)
            {
                MessageBox.Show(this, "Die Datei ist größer als 5 MB. Bitte eine kleinere Datei verwenden.", "Datei zu groß",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            string ext = info.Extension.ToLowerInvariant();
            string mime;
            switch (ext)
            {
                case ".png": mime = "image/png"; break;
                case ".jpg": case ".jpeg": mime = "image/jpeg"; break;
                case ".gif": mime = "image/gif"; break;
                case ".webp": mime = "image/webp"; break;
                case ".svg": mime = "image/svg+xml"; break;
                case ".mp3": mime = "audio/mpeg"; break;
                case ".wav": mime = "audio/wav"; break;
                case ".ogg": mime = "audio/ogg"; break;
                default: mime = "application/octet-stream"; break;
            }
            return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
        }
    }

    void PlayDataUri(string dataUri)
    {
        try
        {
            if (string.IsNullOrEmpty(dataUri)) return;
            string ext = dataUri.StartsWith("data:audio/wav") ? ".wav" : dataUri.StartsWith("data:audio/ogg") ? ".ogg" : ".mp3";
            string tmp = Path.Combine(Path.GetTempPath(), "gluecksrad_preview" + ext);
            File.WriteAllBytes(tmp, Convert.FromBase64String(dataUri.Substring(dataUri.IndexOf(',') + 1)));
            System.Diagnostics.Process.Start(tmp);
        }
        catch (Exception ex) { SetStatus("Sound kann nicht abgespielt werden: " + ex.Message); }
    }

    // ================= Hilfen =================
    static string ToHex(Color c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }

    static Color FromHex(string hex)
    {
        try
        {
            if (string.IsNullOrEmpty(hex)) return Color.Gray;
            if (!hex.StartsWith("#")) return Color.FromName(hex);
            if (hex.Length == 4) hex = "#" + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
            return Color.FromArgb(Convert.ToInt32(hex.Substring(1, 2), 16), Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
        }
        catch { return Color.Gray; }
    }

    static decimal Clamp(decimal v, NumericUpDown n) { return Math.Max(n.Minimum, Math.Min(n.Maximum, v)); }

    static TableLayoutPanel NewTable()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(S(4)) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(190)));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static void AddRow(TableLayoutPanel t, string label, Control c)
    {
        int row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(S(0), S(6), S(0), S(0)) };
        c.Margin = new Padding(S(3), S(4), S(3), S(4));
        t.Controls.Add(l, 0, row);
        t.Controls.Add(c, 1, row);
    }

    static Label Hint(string text)
    {
        return new Label { Text = text, AutoSize = true, MaximumSize = new Size(S(620), S(0)), ForeColor = Color.DimGray };
    }

    Label Header(string text)
    {
        return new Label { Text = text, AutoSize = true, Font = new Font(Font.FontFamily, 10f, FontStyle.Bold), Padding = new Padding(S(0), S(12), S(0), S(2)) };
    }

    static CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true }; }

    static NumericUpDown Num(decimal min, decimal max, int decimals)
    {
        return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = S(110), Increment = decimals > 0 ? 0.5m : 1m };
    }

    static Control Pair(CheckBox box, NumericUpDown num, string suffix)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        box.Margin = new Padding(S(0), S(5), S(6), S(0));
        p.Controls.Add(box);
        p.Controls.Add(num);
        p.Controls.Add(new Label { Text = suffix, AutoSize = true, Padding = new Padding(S(4), S(6), S(0), S(0)) });
        return p;
    }

    Button ColorButton()
    {
        var b = new Button { Width = S(110), Height = S(26), FlatStyle = FlatStyle.Flat, Text = "" };
        b.Click += (s, e) =>
        {
            using (var dlg = new ColorDialog { FullOpen = true, Color = b.BackColor })
                if (dlg.ShowDialog(this) == DialogResult.OK) b.BackColor = dlg.Color;
        };
        return b;
    }

    static Button MakeButton(string text, int width, EventHandler click)
    {
        var b = new Button { Text = text, Width = width, Height = S(30) };
        b.Click += click;
        return b;
    }

    static DataGridViewTextBoxColumn TextCol(string header, int width, bool readOnly)
    {
        return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, ReadOnly = readOnly };
    }

    class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
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
    public string pointerColor { get; set; }
    public string centerColor { get; set; }
    public bool proportional { get; set; }
    public int fontSize { get; set; }
    public bool showBanner { get; set; }
    public double holdSeconds { get; set; }
    public bool idleVisible { get; set; }
    public bool tick { get; set; }
    public double volume { get; set; }

    public GrLook()
    {
        spinSeconds = 12; spins = 8; borderColor = "#ffffff"; borderWidth = 6; pointerColor = "#ffcc00";
        centerColor = "#ffffff"; proportional = true; fontSize = 28; showBanner = true; holdSeconds = 6;
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
    public List<GrWheel> wheels { get; set; }

    public GrConfig() { version = 1; host = "127.0.0.1"; port = 8080; obsConnection = 0; chatAsBot = true; wheels = new List<GrWheel>(); }
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
        boot["wheel"] = w;
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
    public static void StartSpin(IInlineInvokeProxy cph, GrWheel w, string user, bool test)
    {
        if (w.segments.Count == 0) { cph.LogWarn("[Glücksrad] Rad '" + w.name + "' hat keine Felder."); return; }
        int idx = PickSegment(w);
        string spinId = Guid.NewGuid().ToString("N");
        // Ergebnis serverseitig merken: nur das erste Ergebnis-Event zählt (z.B. bei doppelt geladener Quelle)
        cph.SetGlobalVar(PendingPrefix + spinId, w.id + "|" + idx + "|" + (test ? "1" : "0") + "|" + user, false);

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
            ObsRequest(cph, cfg, "PressInputPropertiesButton", D("inputName", input, "propertyName", "refreshnocache"));
            log.Add("Browser-Quelle aktualisiert");
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
