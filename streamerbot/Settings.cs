// Action "Glücksrad – Einstellungen"
// Öffnet das Einstellungsfenster. Hier werden Räder, Felder, Auslöser, Aussehen und OBS eingerichtet.
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

public class CPHInline
{
    // Wird von tools/build_import.py durch den Inhalt von overlay/overlay.html ersetzt.
    const string OverlayTemplate = ""; //GR_OVERLAY_TEMPLATE

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
