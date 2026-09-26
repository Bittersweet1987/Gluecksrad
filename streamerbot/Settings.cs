// Action "Glücksrad – Einstellungen"
// Öffnet das Einstellungsfenster. Hier werden Räder, Felder, Auslöser, Aussehen und OBS eingerichtet.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

// ===================== Farben für Hell- und Dunkelmodus =====================
public class GrTheme
{
    public bool Dark;
    public Color Back, Surface, Input, Border, Text, Dim, Accent, AccentText, Header, Selection, SelectionText, Button;

    public static GrTheme Get(bool dark)
    {
        var t = new GrTheme { Dark = dark };
        if (dark)
        {
            t.Back = Color.FromArgb(32, 32, 32); t.Surface = Color.FromArgb(43, 43, 43); t.Input = Color.FromArgb(56, 56, 56);
            t.Border = Color.FromArgb(80, 80, 80); t.Text = Color.FromArgb(242, 242, 242); t.Dim = Color.FromArgb(170, 170, 170);
            t.Accent = Color.FromArgb(58, 123, 213); t.AccentText = Color.White; t.Header = Color.FromArgb(50, 50, 50);
            t.Selection = Color.FromArgb(58, 95, 143); t.SelectionText = Color.White; t.Button = Color.FromArgb(62, 62, 62);
        }
        else
        {
            t.Back = Color.FromArgb(243, 243, 243); t.Surface = Color.White; t.Input = Color.White;
            t.Border = Color.FromArgb(200, 200, 200); t.Text = Color.FromArgb(30, 30, 30); t.Dim = Color.FromArgb(100, 100, 100);
            t.Accent = Color.FromArgb(43, 108, 196); t.AccentText = Color.White; t.Header = Color.FromArgb(232, 232, 232);
            t.Selection = Color.FromArgb(204, 224, 255); t.SelectionText = Color.Black; t.Button = Color.FromArgb(228, 228, 228);
        }
        return t;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Dunkle Titelleiste (Windows 10 20H1+ / Windows 11)
    public static void TitleBar(Form f, bool dark)
    {
        try { int v = dark ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
    }

    public void Apply(Control root)
    {
        root.BackColor = Back;
        root.ForeColor = Text;
        foreach (Control c in root.Controls) ApplyOne(c);
    }

    void ApplyOne(Control c)
    {
        string tag = c.Tag as string;
        if (tag == "swatch") { /* Farbfeld behält seine Farbe */ }
        else if (c is Button)
        {
            var b = (Button)c;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.BackColor = tag == "primary" ? Accent : Button;
            b.ForeColor = tag == "primary" ? AccentText : Text;
            b.UseVisualStyleBackColor = false;
        }
        else if (c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button)
        {
            var r = (RadioButton)c;
            r.FlatStyle = FlatStyle.Flat;
            r.FlatAppearance.BorderColor = Back;
            r.FlatAppearance.CheckedBackColor = Accent;
            r.BackColor = Back;
            r.ForeColor = r.Checked ? AccentText : Text;
        }
        else if (c is TextBox || c is NumericUpDown || c is ListBox || c is ComboBox)
        {
            c.BackColor = Input;
            c.ForeColor = Text;
            if (c is ComboBox) ((ComboBox)c).FlatStyle = FlatStyle.Flat;
            if (c is TextBox) ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
            if (c is NumericUpDown) ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle;
            if (c is ListBox) ((ListBox)c).BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is DataGridView)
        {
            var g = (DataGridView)c;
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Surface;
            g.GridColor = Border;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = Selection;
            g.DefaultCellStyle.SelectionForeColor = SelectionText;
            g.ColumnHeadersDefaultCellStyle.BackColor = Header;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Text;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Header;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        }
        else if (c is PictureBox) { c.BackColor = Input; }
        else if (tag == "preview") { /* Vorschau hat eigenen Hintergrund */ }
        else
        {
            // Container und Beschriftungen übernehmen den Hintergrund ihres Elternelements
            c.BackColor = tag == "surface" ? Surface : (c.Parent != null ? c.Parent.BackColor : Back);
            c.ForeColor = tag == "dim" ? Dim : Text;
        }
        foreach (Control child in c.Controls) ApplyOne(child);
    }
}

// ===================== Durchsuchbare Action-Auswahl =====================
public class GrActionPicker : Form
{
    public string Selected;
    readonly List<string[]> all;   // [Gruppe, Name]
    readonly TextBox search;
    readonly ListBox list;

    public GrActionPicker(List<string[]> actions, string current, GrTheme theme, Func<int, int> S)
    {
        all = actions;
        Text = "Gewinn-Action auswählen";
        Font = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(S(560), S(620));
        MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
        Padding = new Padding(S(10));

        search = new TextBox { Dock = DockStyle.Top };
        var hint = new Label { Text = "Suchen (Name oder Gruppe):", Dock = DockStyle.Top, Height = S(24), Tag = "dim" };
        list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(48), FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, S(8), 0, 0) };
        var ok = new Button { Text = "Übernehmen", Width = S(130), Height = S(32), Tag = "primary" };
        var cancel = new Button { Text = "Abbrechen", Width = S(110), Height = S(32) };
        var none = new Button { Text = "Keine Action", Width = S(120), Height = S(32) };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel); buttons.Controls.Add(none);
        var spacer = new Panel { Dock = DockStyle.Top, Height = S(8) };

        Controls.Add(list);
        Controls.Add(spacer);
        Controls.Add(search);
        Controls.Add(hint);
        Controls.Add(buttons);

        search.TextChanged += (s, e) => Fill(null);
        search.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.Items.Count - 1); e.Handled = true; }
            if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; e.SuppressKeyPress = true; }
        };
        list.DoubleClick += (s, e) => Accept();
        list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Accept(); };
        ok.Click += (s, e) => Accept();
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        none.Click += (s, e) => { Selected = ""; DialogResult = DialogResult.OK; Close(); };

        theme.Apply(this);
        BackColor = theme.Back; ForeColor = theme.Text;
        HandleCreated += (s, e) => GrTheme.TitleBar(this, theme.Dark);
        Fill(current);
        Shown += (s, e) => search.Focus();
    }

    class Item
    {
        public string Name, Label;
        public override string ToString() { return Label; }
    }

    void Fill(string select)
    {
        string q = search.Text.Trim();
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var a in all)
        {
            string label = (a[0] != "" ? a[0] + "  ›  " : "") + a[1];
            if (q != "" && label.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            int i = list.Items.Add(new Item { Name = a[1], Label = label });
            if (select != null && a[1] == select) list.SelectedIndex = i;
        }
        if (list.SelectedIndex < 0 && list.Items.Count > 0 && q != "") list.SelectedIndex = 0;
        list.EndUpdate();
    }

    void Accept()
    {
        var item = list.SelectedItem as Item;
        if (item == null) return;
        Selected = item.Name;
        DialogResult = DialogResult.OK;
        Close();
    }
}

// ===================== Einstellungsfenster =====================
public class GrSettingsForm : Form
{
    readonly IInlineInvokeProxy cph;
    readonly string overlayTemplate;
    GrConfig cfg;
    GrWheel wheel;
    GrTheme theme;
    bool loading;
    bool dirty;
    List<string[]> actions = new List<string[]>();
    List<TwitchReward> rewards = new List<TwitchReward>();
    readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();

    // Grundgerüst
    ListBox wheelList;
    Panel pageHost, preview;
    readonly List<Panel> pages = new List<Panel>();
    readonly List<RadioButton> navButtons = new List<RadioButton>();
    Button themeButton;
    Label statusLabel;
    CheckBox testActionsBox;
    // Allgemein
    TextBox nameBox, hostBox;
    CheckBox enabledBox, chatAsBotBox;
    NumericUpDown portBox, obsConnBox;
    // Felder
    DataGridView grid;
    Label sumLabel;
    TextBox segTextBox, segChatBox, segActionBox;
    PictureBox segImageBox;
    Label segSoundLabel, segTitle, segPreviewLabel;
    string previewUser;
    readonly ToolTip tips = new ToolTip();
    CheckBox segFreeBox;
    Panel detail;
    // Auslöser
    CheckedListBox rewardList;
    CheckBox bitsBox, subsBox, resubsBox, giftBox, tipsBox;
    NumericUpDown bitsMin, giftMin, tipMin;
    TextBox commandBox;
    // Aussehen
    NumericUpDown spinSeconds, spins, holdSeconds, blinkMs, volume, borderWidth, lineWidth, innerRadius, hubSize, fontSize, imageSize, imageDistance;
    Button borderColorBtn, lineColorBtn, pointerColorBtn, centerColorBtn, winColorBtn;
    CheckBox proportionalBox, bannerBox, idleVisibleBox, tickBox, showTextBox, showPointerBox;
    ComboBox imageModeBox;
    PictureBox centerImageBox;
    // OBS
    Label obsSceneLabel, obsInputLabel, obsStatusLabel;
    TextBox filePathBox;
    NumericUpDown obsWidth, obsHeight;
    ComboBox addToSceneBox;

    const int ColText = 0, ColColor = 1, ColTextColor = 2, ColImage = 3, ColWeight = 4, ColPercent = 5, ColAction = 6, ColFree = 7;

    static readonly string[][] ImageModes =
    {
        new[] { "inward", "Oberkante zur Mitte" },
        new[] { "outward", "Oberkante nach außen" },
        new[] { "left", "Quer (Oberkante links)" },
        new[] { "right", "Quer (Oberkante rechts)" },
        new[] { "upright", "Immer aufrecht" }
    };

    public GrSettingsForm(IInlineInvokeProxy cph, string overlayTemplate)
    {
        this.cph = cph;
        this.overlayTemplate = overlayTemplate;
        cfg = GrCore.Load(cph);
        if (cfg.wheels.Count == 0) { cfg.wheels.Add(GrCore.ExampleWheel()); dirty = true; }
        theme = GrTheme.Get(cfg.darkMode);

        LoadStreamerbotLists();
        DetectScale();
        BuildUi();
        ApplyTheme();
        FitToScreen();
        ShowPage(1);
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
            actions = cph.GetActions()
                .Where(a => a.Name != GrCore.SettingsActionName && a.Name != GrCore.SpinActionName && a.Name != GrCore.ResultActionName)
                .OrderBy(a => a.Group ?? "", StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(a => new[] { a.Group ?? "", a.Name }).ToList();
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

    string TestUser()
    {
        try
        {
            var b = cph.TwitchGetBroadcaster();
            if (b != null && !string.IsNullOrEmpty(b.UserName)) return b.UserName;
        }
        catch { }
        return "Test";
    }

    // ================= Aufbau der Oberfläche =================
    void BuildUi()
    {
        Text = "Glücksrad – Einstellungen";
        Font = new Font("Segoe UI", 10f);
        Size = new Size(S(1500), S(880));
        MinimumSize = new Size(S(1150), S(700));
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
        Shown += (s, e) => { TopMost = true; Activate(); TopMost = false; };
        HandleCreated += (s, e) => GrTheme.TitleBar(this, theme.Dark);

        // Mitte: Navigation + Seiten
        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(S(4), S(8), S(4), 0) };
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(46), WrapContents = false };
        string[] names = { "Allgemein", "Felder", "Auslöser", "Aussehen", "OBS" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var rb = new RadioButton
            {
                Text = names[i], Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter,
                Width = S(118), Height = S(36), Margin = new Padding(0, 0, S(4), 0), Font = new Font("Segoe UI", 10.5f)
            };
            rb.CheckedChanged += (s, e) => { if (rb.Checked) ShowPage(index); };
            navButtons.Add(rb);
            nav.Controls.Add(rb);
        }
        themeButton = MakeButton("", S(150), (s, e) => ToggleTheme());
        themeButton.Margin = new Padding(S(24), 0, 0, 0);
        themeButton.Height = S(36);
        nav.Controls.Add(themeButton);

        pageHost = new Panel { Dock = DockStyle.Fill, Tag = "surface" };
        pages.Add(BuildGeneralPage());
        pages.Add(BuildSegmentsPage());
        pages.Add(BuildTriggersPage());
        pages.Add(BuildLookPage());
        pages.Add(BuildObsPage());
        foreach (var p in pages) { p.Dock = DockStyle.Fill; p.Visible = false; pageHost.Controls.Add(p); }
        center.Controls.Add(pageHost);
        center.Controls.Add(nav);

        // Rechte Seite: Vorschau
        var right = new Panel { Dock = DockStyle.Right, Width = S(360), Padding = new Padding(S(8), S(8), S(10), S(8)) };
        var previewTitle = new Label { Text = "Vorschau", Dock = DockStyle.Top, Height = S(30), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        preview = new DoubleBufferedPanel { Dock = DockStyle.Fill, Tag = "preview" };
        preview.Paint += PaintPreview;
        preview.Resize += (s, e) => preview.Invalidate();
        right.Controls.Add(preview);
        right.Controls.Add(previewTitle);

        // Linke Seite: Liste der Räder
        var left = new Panel { Dock = DockStyle.Left, Width = S(236), Padding = new Padding(S(10), S(8), S(4), S(8)) };
        var leftTitle = new Label { Text = "Glücksräder", Dock = DockStyle.Top, Height = S(30), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        wheelList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Segoe UI", 11f), ItemHeight = S(26), DrawMode = DrawMode.OwnerDrawFixed };
        wheelList.DrawItem += DrawWheelItem;
        wheelList.SelectedIndexChanged += (s, e) => { if (!loading && wheelList.SelectedIndex >= 0) SelectWheel(wheelList.SelectedIndex); };
        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(130), Padding = new Padding(0, S(6), 0, 0) };
        leftButtons.Controls.Add(MakeButton("Neu", S(100), (s, e) => AddWheel(false)));
        leftButtons.Controls.Add(MakeButton("Duplizieren", S(100), (s, e) => AddWheel(true)));
        leftButtons.Controls.Add(MakeButton("Importieren …", S(100), (s, e) => ImportWheel()));
        leftButtons.Controls.Add(MakeButton("Exportieren …", S(100), (s, e) => ExportWheel()));
        leftButtons.Controls.Add(MakeButton("Löschen", S(100), (s, e) => DeleteWheel()));
        left.Controls.Add(wheelList);
        left.Controls.Add(leftButtons);
        left.Controls.Add(leftTitle);

        // Unten: Status und Knöpfe
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = S(58), Padding = new Padding(S(12), S(10), S(12), S(10)) };
        statusLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Tag = "dim" };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = S(700), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        buttons.Controls.Add(MakeButton("Schließen", S(120), (s, e) => Close()));
        var saveBtn = MakeButton("Speichern", S(130), (s, e) => SaveAll(true));
        saveBtn.Tag = "primary";
        saveBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        buttons.Controls.Add(saveBtn);
        buttons.Controls.Add(MakeButton("Test-Drehen", S(130), (s, e) => TestSpin()));
        testActionsBox = new CheckBox { Text = "Gewinn-Actions beim Test ausführen", AutoSize = true, Margin = new Padding(0, S(8), S(12), 0) };
        buttons.Controls.Add(testActionsBox);
        bottom.Controls.Add(statusLabel);
        bottom.Controls.Add(buttons);

        Controls.Add(center);
        Controls.Add(right);
        Controls.Add(left);
        Controls.Add(bottom);
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == index;
        for (int i = 0; i < navButtons.Count; i++)
        {
            if (navButtons[i].Checked != (i == index)) navButtons[i].Checked = i == index;
            navButtons[i].ForeColor = i == index ? theme.AccentText : theme.Text;
        }
    }

    void ApplyTheme()
    {
        theme.Apply(this);
        BackColor = theme.Back;
        ForeColor = theme.Text;
        themeButton.Text = theme.Dark ? "☀  Hellmodus" : "☾  Dunkelmodus";
        preview.BackColor = theme.Dark ? Color.FromArgb(24, 24, 24) : Color.FromArgb(225, 225, 228);
        for (int i = 0; i < navButtons.Count; i++) navButtons[i].ForeColor = navButtons[i].Checked ? theme.AccentText : theme.Text;
        if (IsHandleCreated) GrTheme.TitleBar(this, theme.Dark);
        if (grid != null && wheel != null) { FillGrid(); LoadDetail(); }
        Invalidate(true);
    }

    void ToggleTheme()
    {
        cfg.darkMode = !cfg.darkMode;
        theme = GrTheme.Get(cfg.darkMode);
        ApplyTheme();
        // Nur die Anzeige-Einstellung sofort merken, Rest bleibt ungespeichert
        try
        {
            var stored = GrCore.Load(cph);
            stored.darkMode = cfg.darkMode;
            GrCore.Save(cph, stored);
        }
        catch { }
    }

    Panel NewPage()
    {
        return new Panel { AutoScroll = true, Padding = new Padding(S(16), S(12), S(16), S(12)), Tag = "surface" };
    }

    Panel BuildGeneralPage()
    {
        var page = NewPage();
        var t = NewTable();
        nameBox = new TextBox { Width = S(360) };
        nameBox.TextChanged += (s, e) =>
        {
            if (loading) return;
            wheel.name = nameBox.Text.Trim();
            MarkDirty();
            wheelList.Invalidate();
            UpdateObsLabels();
        };
        enabledBox = new CheckBox { Text = "Rad ist aktiv (reagiert auf Auslöser)", AutoSize = true };
        enabledBox.CheckedChanged += (s, e) => { if (loading) return; wheel.enabled = enabledBox.Checked; MarkDirty(); wheelList.Invalidate(); };
        AddRow(t, "Name des Rads:", nameBox);
        AddRow(t, "", enabledBox);
        AddRow(t, "", Hint("Der Name bestimmt die OBS-Szene „Glücksrad – <Name>“ und die Browser-Quelle darin. " +
                           "Beim Speichern werden beide automatisch angelegt oder aktualisiert."));

        AddRow(t, "", Header("Für alle Räder"));
        chatAsBotBox = new CheckBox { Text = "Chatnachrichten mit dem Bot-Account senden", AutoSize = true };
        chatAsBotBox.CheckedChanged += (s, e) => { if (loading) return; cfg.chatAsBot = chatAsBotBox.Checked; MarkDirty(); };
        AddRow(t, "", chatAsBotBox);
        hostBox = new TextBox { Width = S(180) };
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

    Panel BuildSegmentsPage()
    {
        var page = NewPage();
        page.AutoScroll = false;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = S(44), WrapContents = false };
        toolbar.Controls.Add(MakeButton("+ Feld", S(96), (s, e) => AddSegment()));
        toolbar.Controls.Add(MakeButton("Feld entfernen", S(130), (s, e) => RemoveSegment()));
        toolbar.Controls.Add(MakeButton("▲ Hoch", S(90), (s, e) => MoveSegment(-1)));
        toolbar.Controls.Add(MakeButton("▼ Runter", S(90), (s, e) => MoveSegment(1)));
        sumLabel = new Label { AutoSize = true, Padding = new Padding(S(12), S(8), 0, 0), Tag = "dim" };
        toolbar.Controls.Add(sumLabel);

        grid = new DataGridView
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, EditMode = DataGridViewEditMode.EditOnEnter,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };
        grid.RowTemplate.Height = S(34);
        grid.DefaultCellStyle.Padding = new Padding(S(4), 0, S(4), 0);
        grid.Columns.Add(TextCol("Text / Gewinn", S(190), false));
        grid.Columns.Add(TextCol("Farbe", S(80), true));
        grid.Columns.Add(TextCol("Textfarbe", S(80), true));
        grid.Columns.Add(TextCol("Bild", S(60), true));
        grid.Columns.Add(TextCol("Chance", S(70), false));
        grid.Columns.Add(TextCol("%", S(70), true));
        var actionCol = TextCol("Gewinn-Action", S(200), true);
        actionCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns.Add(actionCol);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Freispin", Width = S(70) });
        foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns[ColImage].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns[ColPercent].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        grid.CellValueChanged += OnGridValueChanged;
        grid.CellClick += OnGridCellClick;
        grid.SelectionChanged += (s, e) => LoadDetail();
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.DataError += (s, e) => { e.ThrowException = false; };
        grid.EditingControlShowing += (s, e) => { e.Control.BackColor = theme.Input; e.Control.ForeColor = theme.Text; };

        detail = BuildDetailPanel();
        detail.Dock = DockStyle.Bottom;

        page.Controls.Add(grid);
        page.Controls.Add(toolbar);
        page.Controls.Add(detail);
        return page;
    }

    Panel BuildDetailPanel()
    {
        var p = new Panel { Height = S(400), Padding = new Padding(0, S(10), 0, 0) };
        segTitle = new Label { Dock = DockStyle.Top, Height = S(30), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 6 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(150)));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(260)));
        for (int i = 0; i < 6; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        segTextBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        segTextBox.TextChanged += (s, e) => { var sg = CurrentSegment(); if (loading || sg == null) return; sg.text = segTextBox.Text; RefreshCurrentRow(); UpdateChatPreview(); };
        segChatBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Multiline = true, Height = S(58), ScrollBars = ScrollBars.Vertical };
        segChatBox.TextChanged += (s, e) => { var sg = CurrentSegment(); if (loading || sg == null) return; sg.chat = segChatBox.Text.Replace("\r", "").Replace("\n", " "); MarkDirty(); UpdateChatPreview(); };
        segActionBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true };
        var actionButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        actionButtons.Controls.Add(MakeButton("Auswählen …", S(120), (s, e) => PickAction()));
        actionButtons.Controls.Add(MakeButton("Entfernen", S(100), (s, e) => { var sg = CurrentSegment(); if (sg == null) return; sg.action = ""; RefreshCurrentRow(); LoadDetail(); }));
        segFreeBox = new CheckBox { Text = "Freispin (Gewinner dreht sofort nochmal)", AutoSize = true };
        segFreeBox.CheckedChanged += (s, e) => { var sg = CurrentSegment(); if (loading || sg == null) return; sg.freeSpin = segFreeBox.Checked; RefreshCurrentRow(); };
        segSoundLabel = new Label { AutoSize = true, Padding = new Padding(0, S(6), S(10), 0) };
        var soundButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        soundButtons.Controls.Add(segSoundLabel);
        soundButtons.Controls.Add(MakeButton("Sound wählen …", S(140), (s, e) => { var sg = CurrentSegment(); if (sg == null) return; string u = PickFileAsDataUri(false); if (u == null) return; sg.sound = u; RefreshCurrentRow(); LoadDetail(); }));
        soundButtons.Controls.Add(MakeButton("Anhören", S(90), (s, e) => { var sg = CurrentSegment(); if (sg != null) PlayDataUri(sg.sound); }));
        soundButtons.Controls.Add(MakeButton("Entfernen", S(100), (s, e) => { var sg = CurrentSegment(); if (sg == null) return; sg.sound = ""; RefreshCurrentRow(); LoadDetail(); }));

        segImageBox = new PictureBox { Width = S(110), Height = S(110), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        var imageButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        imageButtons.Controls.Add(MakeButton("Bild wählen …", S(130), (s, e) => { var sg = CurrentSegment(); if (sg == null) return; string u = PickFileAsDataUri(true); if (u == null) return; sg.image = u; RefreshCurrentRow(); LoadDetail(); }));
        imageButtons.Controls.Add(MakeButton("Bild entfernen", S(130), (s, e) => { var sg = CurrentSegment(); if (sg == null) return; sg.image = ""; RefreshCurrentRow(); LoadDetail(); }));
        segFreeBox.Text = "Freispin";
        segFreeBox.Margin = new Padding(S(2), S(8), 0, 0);
        imageButtons.Controls.Add(segFreeBox);
        var imagePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(S(10), 0, 0, 0) };
        imagePanel.Controls.Add(segImageBox);
        imagePanel.Controls.Add(imageButtons);

        AddCell(t, 0, 0, new Label { Text = "Text / Gewinn:", AutoSize = true, Anchor = AnchorStyles.Left });
        AddCell(t, 1, 0, segTextBox);
        AddCell(t, 0, 1, new Label { Text = "Chatnachricht:", AutoSize = true, Anchor = AnchorStyles.Left });
        AddCell(t, 1, 1, segChatBox);
        // Variablen per Klick einfügen, damit niemand die Platzhalter kennen muss
        var varButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0), MaximumSize = new Size(S(760), 0) };
        foreach (var v in GrCore.ChatVariables)
        {
            string placeholder = v[0];
            var b = new Button { Text = "+ " + v[1], AutoSize = true, Height = S(30), Margin = new Padding(0, 0, S(6), S(4)), Font = new Font("Segoe UI", 9f) };
            b.Click += (s, e) => InsertVariable(placeholder);
            tips.SetToolTip(b, v[2] + "  →  " + placeholder);
            varButtons.Controls.Add(b);
        }
        segPreviewLabel = new Label { AutoSize = true, MaximumSize = new Size(S(760), 0), Tag = "dim" };
        AddCell(t, 0, 2, new Label { Text = "Einfügen:", AutoSize = true, Anchor = AnchorStyles.Left });
        AddCell(t, 1, 2, varButtons);
        AddCell(t, 0, 3, new Label { Text = "Vorschau:", AutoSize = true, Anchor = AnchorStyles.Left });
        AddCell(t, 1, 3, segPreviewLabel);
        AddCell(t, 0, 4, new Label { Text = "Gewinn-Action:", AutoSize = true, Anchor = AnchorStyles.Left });
        var actionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionRow.Controls.Add(segActionBox, 0, 0);
        actionRow.Controls.Add(actionButtons, 1, 0);
        AddCell(t, 1, 4, actionRow);
        AddCell(t, 0, 5, new Label { Text = "Sound:", AutoSize = true, Anchor = AnchorStyles.Left });
        AddCell(t, 1, 5, soundButtons);
        AddCell(t, 2, 0, imagePanel);
        t.SetRowSpan(imagePanel, 6);

        var hint = Hint("Tipp: Mit den Knöpfen unter „Einfügen“ setzt du Gewinner, Gewinn usw. an der Cursorposition ein. " +
                        "Die Gewinn-Action kennt dieselben Werte als %user%, %gluecksradPrize%, %gluecksradWheel%, %gluecksradAmount% und %gluecksradTrigger%.");
        hint.Dock = DockStyle.Bottom;
        p.Controls.Add(t);
        p.Controls.Add(segTitle);
        p.Controls.Add(hint);
        return p;
    }

    static void AddCell(TableLayoutPanel t, int col, int row, Control c)
    {
        c.Margin = new Padding(S(3), S(4), S(3), S(4));
        t.Controls.Add(c, col, row);
    }

    Panel BuildTriggersPage()
    {
        var page = NewPage();
        var t = NewTable();

        AddRow(t, "", Hint("Wichtig: Die passenden Trigger müssen in Streamer.bot an die Action „Glücksrad – Drehen“ angehängt sein " +
                           "(Twitch → Channel Reward → Reward Redemption, Twitch → Chat → Cheer, " +
                           "Twitch → Subscriptions → Subscription/Resubscription/Gift Subscription/Gift Bomb, Tipps unter Integrations, " +
                           "Befehle unter Core → Commands). Hier legst du fest, welches Rad sich bei welchem Ereignis dreht."));

        AddRow(t, "", Header("Kanalpunkte"));
        rewardList = new CheckedListBox { Width = S(520), Height = S(220), CheckOnClick = true, IntegralHeight = false };
        rewardList.ItemCheck += OnRewardCheck;
        AddRow(t, "Belohnungen:", rewardList);
        AddRow(t, "", MakeButton("Belohnungen neu laden", S(200), (s, e) => { LoadRewards(); FillRewards(); }));

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
        commandBox = new TextBox { Width = S(180) };
        AddRow(t, "Befehl:", commandBox);
        AddRow(t, "", Hint("Den Befehl (z. B. !rad) zusätzlich in Streamer.bot unter Commands anlegen und als Trigger an „Glücksrad – Drehen“ hängen."));

        EventHandler changed = (s, e) =>
        {
            if (loading || wheel == null) return;
            var tr = wheel.triggers;
            tr.bits = bitsBox.Checked; tr.bitsMin = (int)bitsMin.Value;
            tr.subs = subsBox.Checked; tr.resubs = resubsBox.Checked;
            tr.giftSubs = giftBox.Checked; tr.giftMin = (int)giftMin.Value;
            tr.tips = tipsBox.Checked; tr.tipMin = (double)tipMin.Value;
            tr.command = commandBox.Text.Trim();
            MarkDirty();
        };
        foreach (var c in new[] { bitsBox, subsBox, resubsBox, giftBox, tipsBox }) c.CheckedChanged += changed;
        bitsMin.ValueChanged += changed; giftMin.ValueChanged += changed; tipMin.ValueChanged += changed;
        commandBox.TextChanged += changed;

        page.Controls.Add(t);
        return page;
    }

    Panel BuildLookPage()
    {
        var page = NewPage();
        var t = NewTable();
        spinSeconds = Num(2, 120, 1);
        spins = Num(1, 60, 0);
        holdSeconds = Num(1, 120, 1);
        blinkMs = Num(80, 2000, 0);
        volume = Num(0, 100, 0);
        borderWidth = Num(0, 40, 0);
        lineWidth = Num(0, 20, 0);
        innerRadius = Num(0, 80, 0);
        hubSize = Num(0, 60, 0);
        fontSize = Num(8, 120, 0);
        imageSize = Num(5, 100, 0);
        imageDistance = Num(0, 95, 0);
        borderColorBtn = ColorButton(); lineColorBtn = ColorButton(); pointerColorBtn = ColorButton();
        centerColorBtn = ColorButton(); winColorBtn = ColorButton();
        proportionalBox = Check("Feldgröße entspricht der Chance (sonst alle Felder gleich groß)");
        bannerBox = Check("Gewinner-Einblendung unter dem Rad anzeigen");
        idleVisibleBox = Check("Rad auch ohne Drehung dauerhaft anzeigen");
        tickBox = Check("Tick-Geräusch beim Drehen");
        showTextBox = Check("Texte auf dem Rad anzeigen (aus = nur Bilder)");
        showPointerBox = Check("Zeiger oben anzeigen");
        imageModeBox = new ComboBox { Width = S(260), DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var m in ImageModes) imageModeBox.Items.Add(m[1]);
        centerImageBox = new PictureBox { Width = S(64), Height = S(64), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        var centerImagePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        centerImagePanel.Controls.Add(centerImageBox);
        centerImagePanel.Controls.Add(MakeButton("Bild wählen …", S(130), (s, e) => { string u = PickFileAsDataUri(true); if (u == null) return; wheel.look.centerImage = u; LookChanged(); }));
        centerImagePanel.Controls.Add(MakeButton("Entfernen", S(100), (s, e) => { wheel.look.centerImage = ""; LookChanged(); }));

        AddRow(t, "", Header("Drehung"));
        AddRow(t, "Drehdauer (Sekunden):", spinSeconds);
        AddRow(t, "Umdrehungen:", spins);
        AddRow(t, "Anzeige nach Gewinn (Sek.):", holdSeconds);
        AddRow(t, "Blinken des Gewinnfelds (ms):", blinkMs);
        AddRow(t, "Gewinnfarbe (blinkt mit Weiß):", winColorBtn);
        AddRow(t, "", tickBox);
        AddRow(t, "Lautstärke (%):", volume);
        AddRow(t, "", proportionalBox);

        AddRow(t, "", Header("Rad"));
        AddRow(t, "Randbreite:", borderWidth);
        AddRow(t, "Randfarbe:", borderColorBtn);
        AddRow(t, "Trennlinien-Breite:", lineWidth);
        AddRow(t, "Trennlinien-Farbe:", lineColorBtn);
        AddRow(t, "Hohle Mitte (% vom Radius):", innerRadius);
        AddRow(t, "Nabe (% vom Radius):", hubSize);
        AddRow(t, "Farbe der Nabe:", centerColorBtn);
        AddRow(t, "Bild in der Mitte:", centerImagePanel);
        AddRow(t, "", showPointerBox);
        AddRow(t, "Zeigerfarbe:", pointerColorBtn);

        AddRow(t, "", Header("Texte und Bilder auf den Feldern"));
        AddRow(t, "", showTextBox);
        AddRow(t, "Schriftgröße:", fontSize);
        AddRow(t, "Bild-Ausrichtung:", imageModeBox);
        AddRow(t, "Bildgröße (% vom Radius):", imageSize);
        AddRow(t, "Abstand zur Mitte (%):", imageDistance);
        AddRow(t, "", Hint("Für Bilder, die wie ein Tortenstück gestaltet sind, passt meist „Oberkante zur Mitte“. " +
                           "Bildgröße = Länge des Bildes vom inneren zum äußeren Ende."));

        AddRow(t, "", Header("Einblendung"));
        AddRow(t, "", bannerBox);
        AddRow(t, "", idleVisibleBox);

        EventHandler changed = (s, e) =>
        {
            if (loading || wheel == null) return;
            var l = wheel.look;
            l.spinSeconds = (double)spinSeconds.Value; l.spins = (int)spins.Value; l.holdSeconds = (double)holdSeconds.Value;
            l.blinkMs = (int)blinkMs.Value; l.volume = (double)volume.Value / 100.0;
            l.borderWidth = (int)borderWidth.Value; l.lineWidth = (int)lineWidth.Value;
            l.innerRadius = (int)innerRadius.Value; l.hubSize = (int)hubSize.Value; l.fontSize = (int)fontSize.Value;
            l.imageSize = (int)imageSize.Value; l.imageDistance = (int)imageDistance.Value;
            l.borderColor = ToHex(borderColorBtn.BackColor); l.lineColor = ToHex(lineColorBtn.BackColor);
            l.pointerColor = ToHex(pointerColorBtn.BackColor); l.centerColor = ToHex(centerColorBtn.BackColor);
            l.winColor = ToHex(winColorBtn.BackColor);
            l.proportional = proportionalBox.Checked; l.showBanner = bannerBox.Checked;
            l.idleVisible = idleVisibleBox.Checked; l.tick = tickBox.Checked;
            l.showText = showTextBox.Checked; l.showPointer = showPointerBox.Checked;
            if (imageModeBox.SelectedIndex >= 0) l.imageMode = ImageModes[imageModeBox.SelectedIndex][0];
            LookChanged();
        };
        foreach (var n in new[] { spinSeconds, spins, holdSeconds, blinkMs, volume, borderWidth, lineWidth, innerRadius, hubSize, fontSize, imageSize, imageDistance })
            n.ValueChanged += changed;
        foreach (var b in new[] { borderColorBtn, lineColorBtn, pointerColorBtn, centerColorBtn, winColorBtn }) b.BackColorChanged += changed;
        foreach (var c in new[] { proportionalBox, bannerBox, idleVisibleBox, tickBox, showTextBox, showPointerBox }) c.CheckedChanged += changed;
        imageModeBox.SelectedIndexChanged += changed;

        page.Controls.Add(t);
        return page;
    }

    void LookChanged()
    {
        MarkDirty();
        UpdatePercentages();
        centerImageBox.Image = GetImage(wheel.look.centerImage);
        preview.Invalidate();
    }

    Panel BuildObsPage()
    {
        var page = NewPage();
        var t = NewTable();
        AddRow(t, "", Hint("Beim Speichern legt das Glücksrad in OBS automatisch eine eigene Szene mit einer Browser-Quelle an " +
                           "und aktualisiert sie bei jeder Änderung. Benennst du das Rad um, wird auch in OBS umbenannt."));
        obsStatusLabel = new Label { AutoSize = true };
        AddRow(t, "OBS-Status:", obsStatusLabel);
        obsSceneLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        obsInputLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        AddRow(t, "Szene:", obsSceneLabel);
        AddRow(t, "Browser-Quelle:", obsInputLabel);
        obsWidth = Num(200, 4000, 0);
        obsHeight = Num(200, 4000, 0);
        AddRow(t, "Breite der Quelle:", obsWidth);
        AddRow(t, "Höhe der Quelle:", obsHeight);
        addToSceneBox = new ComboBox { Width = S(360), DropDownStyle = ComboBoxStyle.DropDownList };
        AddRow(t, "Zusätzlich einfügen in:", addToSceneBox);
        AddRow(t, "", Hint("Wähle z. B. deine Live-Szene: Die Rad-Szene wird dort einmalig als Quelle eingefügt, " +
                           "damit das Rad im Stream erscheint. Das Rad ist nur während einer Drehung sichtbar."));
        AddRow(t, "", MakeButton("Szenenliste neu laden", S(200), (s, e) => FillScenes()));
        filePathBox = new TextBox { Width = S(560), ReadOnly = true };
        AddRow(t, "Overlay-Datei:", filePathBox);
        var fileButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        fileButtons.Controls.Add(MakeButton("Pfad kopieren", S(140), (s, e) => { try { Clipboard.SetText(filePathBox.Text); SetStatus("Pfad kopiert."); } catch { } }));
        fileButtons.Controls.Add(MakeButton("Ordner öffnen", S(140), (s, e) =>
        {
            try { Directory.CreateDirectory(GrCore.OverlayDir()); System.Diagnostics.Process.Start("explorer.exe", GrCore.OverlayDir()); } catch { }
        }));
        fileButtons.Controls.Add(MakeButton("Jetzt mit OBS synchronisieren", S(250), (s, e) => SaveAll(true)));
        AddRow(t, "", fileButtons);

        EventHandler changed = (s, e) =>
        {
            if (loading || wheel == null) return;
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
    void DrawWheelItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= cfg.wheels.Count) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? theme.Selection : theme.Input)) e.Graphics.FillRectangle(b, e.Bounds);
        var w = cfg.wheels[e.Index];
        string text = (string.IsNullOrEmpty(w.name) ? "(ohne Name)" : w.name) + (w.enabled ? "" : "  (aus)");
        TextRenderer.DrawText(e.Graphics, text, wheelList.Font, new Rectangle(e.Bounds.X + S(6), e.Bounds.Y, e.Bounds.Width - S(6), e.Bounds.Height),
            sel ? theme.SelectionText : (w.enabled ? theme.Text : theme.Dim), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    void RefreshWheelList(int select)
    {
        loading = true;
        wheelList.Items.Clear();
        foreach (var w in cfg.wheels) wheelList.Items.Add(w.id);
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
            pageHost.Enabled = false;
            preview.Invalidate();
        }
    }

    void SelectWheel(int index)
    {
        if (grid.IsCurrentCellInEditMode) grid.EndEdit();
        wheel = cfg.wheels[index];
        pageHost.Enabled = true;
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
        holdSeconds.Value = Clamp((decimal)l.holdSeconds, holdSeconds); blinkMs.Value = Clamp(l.blinkMs, blinkMs);
        volume.Value = Clamp((decimal)(l.volume * 100), volume);
        borderWidth.Value = Clamp(l.borderWidth, borderWidth); lineWidth.Value = Clamp(l.lineWidth, lineWidth);
        innerRadius.Value = Clamp(l.innerRadius, innerRadius); hubSize.Value = Clamp(l.hubSize, hubSize);
        fontSize.Value = Clamp(l.fontSize, fontSize);
        imageSize.Value = Clamp(l.imageSize, imageSize); imageDistance.Value = Clamp(l.imageDistance, imageDistance);
        borderColorBtn.BackColor = FromHex(l.borderColor); lineColorBtn.BackColor = FromHex(l.lineColor);
        pointerColorBtn.BackColor = FromHex(l.pointerColor); centerColorBtn.BackColor = FromHex(l.centerColor);
        winColorBtn.BackColor = FromHex(l.winColor);
        proportionalBox.Checked = l.proportional; bannerBox.Checked = l.showBanner;
        idleVisibleBox.Checked = l.idleVisible; tickBox.Checked = l.tick;
        showTextBox.Checked = l.showText; showPointerBox.Checked = l.showPointer;
        int mode = Array.FindIndex(ImageModes, m => m[0] == l.imageMode);
        imageModeBox.SelectedIndex = mode >= 0 ? mode : 1;
        centerImageBox.Image = GetImage(l.centerImage);

        obsWidth.Value = Clamp(wheel.obs.width, obsWidth);
        obsHeight.Value = Clamp(wheel.obs.height, obsHeight);
        FillScenes();

        FillGrid();
        loading = false;
        UpdateObsLabels();
        LoadDetail();
        wheelList.Invalidate();
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
        navButtons[0].Checked = true;
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

    void ExportWheel()
    {
        if (wheel == null) return;
        using (var dlg = new SaveFileDialog())
        {
            dlg.Title = "Rad exportieren";
            dlg.Filter = "Glücksrad (*.json)|*.json";
            dlg.FileName = "Gluecksrad_" + string.Join("_", wheel.name.Split(Path.GetInvalidFileNameChars())) + ".json";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName, GrCore.ExportWheel(wheel), new System.Text.UTF8Encoding(false));
            SetStatus("Rad exportiert: " + dlg.FileName);
        }
    }

    void ImportWheel()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Rad importieren";
            dlg.Filter = "Glücksrad (*.json)|*.json";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            GrWheel w;
            try { w = GrCore.ImportWheel(File.ReadAllText(dlg.FileName)); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Die Datei konnte nicht gelesen werden: " + ex.Message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var existing = cfg.wheels.FirstOrDefault(x => string.Equals(x.name, w.name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                var r = MessageBox.Show(this, "Es gibt schon ein Rad „" + w.name + "“. Soll es ersetzt werden?\n\n" +
                    "Ja = ersetzen (OBS-Szene bleibt erhalten)\nNein = als neues Rad hinzufügen", "Import",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes)
                {
                    w.id = existing.id;
                    w.obs.syncedScene = existing.obs.syncedScene;
                    w.obs.syncedInput = existing.obs.syncedInput;
                    if (w.obs.addToScene == "") w.obs.addToScene = existing.obs.addToScene;
                    int i = cfg.wheels.IndexOf(existing);
                    cfg.wheels[i] = w;
                    MarkDirty();
                    RefreshWheelList(i);
                    SetStatus("Rad „" + w.name + "“ ersetzt. Zum Übernehmen „Speichern“ klicken.");
                    return;
                }
            }
            w.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            w.name = UniqueName(w.name);
            w.obs.syncedScene = ""; w.obs.syncedInput = "";
            foreach (var other in cfg.wheels) foreach (var id in w.triggers.rewardIds) other.triggers.rewardIds.Remove(id);
            cfg.wheels.Add(w);
            MarkDirty();
            RefreshWheelList(cfg.wheels.Count - 1);
            SetStatus("Rad „" + w.name + "“ importiert. Zum Übernehmen „Speichern“ klicken.");
        }
    }

    // ================= Felder =================
    GrSegment CurrentSegment()
    {
        if (wheel == null || grid.CurrentRow == null) return null;
        int i = grid.CurrentRow.Index;
        return i >= 0 && i < wheel.segments.Count ? wheel.segments[i] : null;
    }

    void FillGrid()
    {
        bool l = loading;
        loading = true;
        int keep = grid.CurrentRow != null ? grid.CurrentRow.Index : 0;
        grid.Rows.Clear();
        foreach (var s in wheel.segments)
        {
            int r = grid.Rows.Add();
            FillRow(grid.Rows[r], s);
        }
        if (grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[Math.Max(0, Math.Min(keep, grid.Rows.Count - 1))].Cells[ColText];
        loading = l;
        UpdatePercentages();
    }

    void FillRow(DataGridViewRow row, GrSegment s)
    {
        row.Cells[ColText].Value = s.text;
        SetColorCell(row.Cells[ColColor], s.color);
        SetColorCell(row.Cells[ColTextColor], s.textColor);
        row.Cells[ColImage].Value = s.image != "" ? "✔" : "–";
        row.Cells[ColWeight].Value = s.weight.ToString("0.##", CultureInfo.CurrentCulture);
        row.Cells[ColAction].Value = s.action != "" ? s.action : "–";
        row.Cells[ColFree].Value = s.freeSpin;
    }

    void RefreshCurrentRow()
    {
        var s = CurrentSegment();
        if (s == null) return;
        bool l = loading;
        loading = true;
        FillRow(grid.CurrentRow, s);
        loading = l;
        MarkDirty();
        preview.Invalidate();
    }

    void LoadDetail()
    {
        var s = CurrentSegment();
        bool l = loading;
        loading = true;
        detail.Enabled = s != null;
        if (s != null)
        {
            segTitle.Text = "Feld " + (grid.CurrentRow.Index + 1) + " bearbeiten";
            if (segTextBox.Text != s.text) segTextBox.Text = s.text;
            if (segChatBox.Text != s.chat) segChatBox.Text = s.chat;
            segActionBox.Text = s.action != "" ? s.action : "(keine)";
            segSoundLabel.Text = s.sound != "" ? "✔ Sound gesetzt" : "kein Sound";
            segFreeBox.Checked = s.freeSpin;
            segImageBox.Image = GetImage(s.image);
            UpdateChatPreview();
        }
        else
        {
            segTitle.Text = "Kein Feld ausgewählt";
            segImageBox.Image = null;
        }
        loading = l;
    }

    void InsertVariable(string placeholder)
    {
        var s = CurrentSegment();
        if (s == null) return;
        int pos = Math.Min(segChatBox.SelectionStart, segChatBox.Text.Length);
        string text = segChatBox.Text.Remove(pos, Math.Min(segChatBox.SelectionLength, segChatBox.Text.Length - pos));
        // Leerzeichen ergänzen, damit der Platzhalter nicht an einem Wort klebt
        string insert = placeholder;
        if (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) insert = " " + insert;
        if (pos < text.Length && !char.IsWhiteSpace(text[pos]) && !char.IsPunctuation(text[pos])) insert += " ";
        segChatBox.Text = text.Insert(pos, insert);
        segChatBox.SelectionStart = pos + insert.Length;
        segChatBox.SelectionLength = 0;
        segChatBox.Focus();
    }

    void UpdateChatPreview()
    {
        var s = CurrentSegment();
        if (s == null || segPreviewLabel == null) return;
        if (previewUser == null) previewUser = TestUser();
        bool cp = wheel.triggers.rewardIds.Count > 0;
        var values = new Dictionary<string, string>();
        values["%user%"] = previewUser;
        values["%prize%"] = s.text;
        values["%wheel%"] = wheel.name;
        values["%amount%"] = cp ? "5000" : "500";
        values["%trigger%"] = cp ? "Kanalpunkte" : "Bits";
        values["%field%"] = (grid.CurrentRow.Index + 1).ToString();
        segPreviewLabel.Text = s.chat.Trim() == "" ? "(keine Chatnachricht – es wird nichts gepostet)" : GrCore.FillText(s.chat, values);
    }

    void PickAction()
    {
        var s = CurrentSegment();
        if (s == null) return;
        using (var dlg = new GrActionPicker(actions, s.action, theme, S))
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            s.action = dlg.Selected ?? "";
            RefreshCurrentRow();
            LoadDetail();
        }
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
            case ColText: s.text = str; LoadDetail(); break;
            case ColFree: s.freeSpin = v is bool && (bool)v; LoadDetail(); break;
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
        if (wheel == null || grid.CurrentRow == null) return;
        int i = grid.CurrentRow.Index;
        if (i < 0 || i >= wheel.segments.Count) return;
        wheel.segments.RemoveAt(i);
        FillGrid();
        if (grid.Rows.Count > 0) grid.CurrentCell = grid.Rows[Math.Min(i, grid.Rows.Count - 1)].Cells[ColText];
        LoadDetail();
        MarkDirty();
        preview.Invalidate();
    }

    void MoveSegment(int dir)
    {
        if (wheel == null || grid.CurrentRow == null) return;
        int i = grid.CurrentRow.Index, j = i + dir;
        if (i < 0 || j < 0 || j >= wheel.segments.Count) return;
        var tmp = wheel.segments[i];
        wheel.segments[i] = wheel.segments[j];
        wheel.segments[j] = tmp;
        FillGrid();
        grid.CurrentCell = grid.Rows[j].Cells[ColText];
        LoadDetail();
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
            grid.Rows[i].Cells[ColPercent].Style.ForeColor = theme.Dim;
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
        obsStatusLabel.ForeColor = connected ? Color.FromArgb(60, 180, 90) : Color.FromArgb(230, 80, 80);
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
            var changed = new HashSet<string>();
            foreach (var w in cfg.wheels)
            {
                string path = GrCore.OverlayPath(w);
                string html = GrCore.BuildOverlayHtml(overlayTemplate, cfg, w, resultId);
                string old = File.Exists(path) ? File.ReadAllText(path) : null;
                if (old != html)
                {
                    File.WriteAllText(path, html, new System.Text.UTF8Encoding(false));
                    changed.Add(w.id);
                }
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
                            string r = GrCore.SyncObs(cph, cfg, w, changed.Contains(w.id));
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
        bool runActions = testActionsBox.Checked;
        GrCore.StartSpin(cph, wheel, TestUser(), !runActions);
        SetStatus("Test-Drehung gestartet für „" + wheel.name + "“. Die Szene muss in OBS sichtbar sein. " +
                  (runActions ? "Gewinn-Actions werden ausgeführt." : "Gewinn-Actions werden nicht ausgeführt."));
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

    // ================= Vorschau (gleiche Geometrie wie overlay.html) =================
    void PaintPreview(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        if (wheel == null || wheel.segments.Count == 0) return;

        float size = Math.Min(preview.Width, preview.Height) - S(24);
        if (size < 50) return;
        float cx = preview.Width / 2f, cy = preview.Height / 2f;
        var L = wheel.look;
        float scale = size / 1000f;
        float R = 440 * scale;
        float inner = Math.Min(0.9f, Math.Max(0, L.innerRadius / 100f)) * R;
        var angles = SegmentAngles();

        for (int i = 0; i < wheel.segments.Count; i++)
        {
            var s = wheel.segments[i];
            float start = (float)angles[i][0] - 90f, sweep = (float)angles[i][1];
            if (sweep <= 0) continue;
            using (var path = new GraphicsPath())
            {
                path.AddArc(cx - R, cy - R, 2 * R, 2 * R, start, sweep);
                if (inner > 0) path.AddArc(cx - inner, cy - inner, 2 * inner, 2 * inner, start + sweep, -sweep);
                else path.AddLine(cx + R * (float)Math.Cos((start + sweep) * Math.PI / 180), cy + R * (float)Math.Sin((start + sweep) * Math.PI / 180), cx, cy);
                path.CloseFigure();
                using (var b = new SolidBrush(FromHex(s.color))) g.FillPath(b, path);
                if (L.lineWidth > 0)
                    using (var p = new Pen(FromHex(L.lineColor), Math.Max(1f, L.lineWidth * scale))) g.DrawPath(p, path);
            }

            float mid = start + sweep / 2f;
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(mid);
            var img = GetImage(s.image);
            if (img != null) DrawSegmentImage(g, img, R, L, mid);
            if (L.showText && !string.IsNullOrWhiteSpace(s.text))
            {
                float fs = Math.Max(6f, L.fontSize * scale);
                float tx = R * 0.92f;
                float maxLen = Math.Max(20 * scale, tx - Math.Max(inner, R * 0.15f) - 10 * scale);
                Font f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
                while (g.MeasureString(s.text, f).Width > maxLen && fs > 6f)
                {
                    f.Dispose();
                    fs -= 0.5f;
                    f = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
                }
                var sz = g.MeasureString(s.text, f);
                using (var b = new SolidBrush(FromHex(s.textColor))) g.DrawString(s.text, f, b, tx - sz.Width, -sz.Height / 2);
                f.Dispose();
            }
            g.Restore(state);
        }
        if (L.borderWidth > 0)
            using (var p = new Pen(FromHex(L.borderColor), Math.Max(1f, L.borderWidth * scale))) g.DrawEllipse(p, cx - R, cy - R, 2 * R, 2 * R);

        float hub = inner > 0 ? inner : Math.Max(0, L.hubSize / 100f) * R;
        var centerImg = GetImage(L.centerImage);
        if (hub > 0)
        {
            if (centerImg != null)
            {
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(cx - hub, cy - hub, 2 * hub, 2 * hub);
                    var st = g.Save();
                    g.SetClip(clip);
                    float s2 = hub * 2 / Math.Max(centerImg.Width, centerImg.Height);
                    g.DrawImage(centerImg, cx - centerImg.Width * s2 / 2, cy - centerImg.Height * s2 / 2, centerImg.Width * s2, centerImg.Height * s2);
                    g.Restore(st);
                }
            }
            else if (inner == 0)
                using (var b = new SolidBrush(FromHex(L.centerColor))) g.FillEllipse(b, cx - hub, cy - hub, hub * 2, hub * 2);
        }
        if (L.showPointer)
        {
            var tri = new[] { new PointF(cx, cy - R + 38 * scale), new PointF(cx - 32 * scale, cy - R - 32 * scale), new PointF(cx + 32 * scale, cy - R - 32 * scale) };
            using (var b = new SolidBrush(FromHex(L.pointerColor))) g.FillPolygon(b, tri);
            using (var p = new Pen(Color.FromArgb(150, 0, 0, 0), 2)) g.DrawPolygon(p, tri);
        }
    }

    // Wie drawSegmentImage() im Overlay: +x zeigt von der Mitte nach außen.
    static void DrawSegmentImage(Graphics g, Image img, float R, GrLook L, float midDeg)
    {
        string mode = L.imageMode ?? "outward";
        float len = Math.Max(1, L.imageSize) / 100f * R;
        float start = Math.Max(0, L.imageDistance) / 100f * R;
        float iw = img.Width, ih = img.Height, w, h, radial;
        if (mode == "inward" || mode == "outward") { h = len; w = len * iw / ih; radial = h; }
        else if (mode == "upright") { float s = len / Math.Max(iw, ih); w = iw * s; h = ih * s; radial = len; }
        else { w = len; h = len * ih / iw; radial = w; }
        var st = g.Save();
        g.TranslateTransform(start + radial / 2, 0);
        if (mode == "inward") g.RotateTransform(-90);
        else if (mode == "outward") g.RotateTransform(90);
        else if (mode == "right") g.RotateTransform(180);
        else if (mode == "upright") g.RotateTransform(-midDeg);
        g.DrawImage(img, -w / 2, -h / 2, w, h);
        g.Restore(st);
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
            dlg.Title = image ? "Bild auswählen" : "Sound auswählen";
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
            hex = hex.Trim();
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
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(250)));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static void AddRow(TableLayoutPanel t, string label, Control c)
    {
        int row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, S(4), 0, 0) };
        c.Margin = new Padding(S(3), S(5), S(3), S(5));
        t.Controls.Add(l, 0, row);
        t.Controls.Add(c, 1, row);
    }

    static Label Hint(string text)
    {
        return new Label { Text = text, AutoSize = true, MaximumSize = new Size(S(720), 0), Tag = "dim" };
    }

    static Label Header(string text)
    {
        return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), Padding = new Padding(0, S(14), 0, S(2)) };
    }

    static CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true }; }

    static NumericUpDown Num(decimal min, decimal max, int decimals)
    {
        return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = S(120), Increment = decimals > 0 ? 0.5m : 1m };
    }

    static Control Pair(CheckBox box, NumericUpDown num, string suffix)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        box.Margin = new Padding(0, S(5), S(6), 0);
        p.Controls.Add(box);
        p.Controls.Add(num);
        p.Controls.Add(new Label { Text = suffix, AutoSize = true, Padding = new Padding(S(4), S(6), 0, 0) });
        return p;
    }

    Button ColorButton()
    {
        var b = new Button { Width = S(120), Height = S(30), FlatStyle = FlatStyle.Flat, Text = "", Tag = "swatch", BackColor = Color.Gray };
        b.FlatAppearance.BorderColor = Color.Gray;
        b.Click += (s, e) =>
        {
            using (var dlg = new ColorDialog { FullOpen = true, Color = b.BackColor })
                if (dlg.ShowDialog(this) == DialogResult.OK) b.BackColor = dlg.Color;
        };
        return b;
    }

    static Button MakeButton(string text, int width, EventHandler click)
    {
        var b = new Button { Text = text, Width = width, Height = S(34), Margin = new Padding(0, 0, S(6), S(6)) };
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
