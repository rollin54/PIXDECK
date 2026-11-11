using System.Drawing.Drawing2D;
using System.Text.Json;

namespace PixDeckHub;

/// <summary>
/// Creador de temas: lienzo de 320x240 donde se agregan, mueven y editan elementos.
/// Guarda archivos .pxtheme, los prueba en el equipo y los agrega a "mis temas".
/// </summary>
public sealed class ThemeEditorForm : DarkForm
{
    readonly HubService _hub;
    CustomTheme _theme;
    int _sel = -1;
    string? _path;
    bool _dirty, _grid = true, _previewActive, _suppress;
    readonly List<string> _undo = new(), _redo = new();
    string _lastUndoKey = "";
    DateTime _lastUndoAt;

    readonly EditorCanvas _canvas;
    readonly Panel _props;
    readonly ListBox _layers;
    readonly Label _status;
    readonly NeonButton _gridBtn;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 120 };

    static string DefaultFolder
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PixDeck Temas");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public ThemeEditorForm(HubService hub)
    {
        _hub = hub;
        _theme = ThemeTemplates.Create(1);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "PixDeck Hub · Creador de temas";
        ClientSize = new Size(1240, 720);
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        // ---------- lienzo (llena el centro) ----------
        _canvas = new EditorCanvas(this) { Dock = DockStyle.Fill };
        Controls.Add(_canvas);

        // ---------- panel derecho: propiedades + capas ----------
        var right = new Panel { Dock = DockStyle.Right, Width = 330, BackColor = Ui.Panel, Padding = new Padding(0) };
        _props = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Panel };
        var layersBox = new Panel { Dock = DockStyle.Bottom, Height = 250, BackColor = Ui.Panel };
        layersBox.Controls.Add(Ui.Label("CAPAS  (arriba = al frente)", 12, 8, Ui.Cyan, Ui.Semi(10)));
        _layers = new ListBox
        {
            Left = 12, Top = 32, Width = 306, Height = 164, BackColor = Ui.Bg, ForeColor = Ui.Text,
            BorderStyle = BorderStyle.FixedSingle, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22, Font = Ui.Font(9),
            IntegralHeight = false,
        };
        _layers.DrawItem += DrawLayer;
        _layers.SelectedIndexChanged += (_, _) =>
        {
            if (_suppress || _layers.SelectedIndex < 0) return;
            Select(_theme.Widgets.Count - 1 - _layers.SelectedIndex);
        };
        layersBox.Controls.Add(_layers);
        int bx = 12;
        foreach (var (t, a) in new (string, Action)[] { ("▲ Frente", () => MoveLayer(+1)), ("▼ Atrás", () => MoveLayer(-1)), ("Duplicar", Duplicate), ("Borrar", DeleteSel) })
        {
            var b = new NeonButton { Left = bx, Top = 204, Width = 72, Height = 36, Title = t, TitleSize = 8.5f };
            b.Click += (_, _) => a();
            layersBox.Controls.Add(b);
            bx += 78;
        }
        right.Controls.Add(_props);
        right.Controls.Add(layersBox);
        right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Ui.Border });
        Controls.Add(right);

        // ---------- caja de herramientas ----------
        var left = new Panel { Dock = DockStyle.Left, Width = 150, BackColor = Ui.Panel };
        left.Controls.Add(Ui.Label("ELEMENTOS", 14, 10, Ui.Violet, Ui.Semi(10)));
        var tools = new (string Type, string Title)[]
        {
            ("rect", "▭  Rectángulo"), ("circle", "●  Círculo"), ("line", "╱  Línea"), ("text", "T  Texto"),
            ("value", "42  Valor"), ("arc", "◔  Arco"), ("bar", "▬  Barra"), ("graph", "〰  Gráfica"),
        };
        int ty = 38;
        foreach (var (type, title) in tools)
        {
            var b = new NeonButton { Left = 10, Top = ty, Width = 130, Height = 42, Title = title, TitleSize = 9.5f };
            b.Click += (_, _) => AddWidget(type);
            left.Controls.Add(b);
            ty += 48;
        }
        left.Controls.Add(new Label
        {
            Left = 10, Top = ty + 6, Width = 132, Height = 150, ForeColor = Ui.Dim, Font = Ui.Font(8),
            Text = "Arrastra para mover.\nEsquinas = tamaño.\nFlechas = 1 px\n(Shift = 10 px).\nSupr = borrar.\nCtrl+D = duplicar.\nCtrl+Z / Ctrl+Y.",
        });
        Controls.Add(left);

        // ---------- barra superior ----------
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 58, BackColor = Ui.Bg, Padding = new Padding(10, 9, 10, 0), WrapContents = false,
        };
        NeonButton TB(string t, Action a, int w = 104, Color? acc = null, Color? acc2 = null)
        {
            var b = new NeonButton { Width = w, Height = 40, Title = t, TitleSize = 9.5f, Margin = new Padding(0, 0, 8, 0) };
            if (acc != null) b.Accent = acc.Value;
            if (acc2 != null) b.Accent2 = acc2.Value;
            b.Click += (_, _) => a();
            top.Controls.Add(b);
            return b;
        }
        NeonButton? newBtn = null;
        newBtn = TB("Nuevo ▾", () => ShowTemplates(newBtn!), 96);
        TB("Abrir", OpenFile, 80);
        TB("Guardar", () => SaveFile(false), 88);
        TB("Guardar como", () => SaveFile(true), 116);
        TB("↶ Deshacer", Undo, 104);
        TB("↷ Rehacer", Redo, 96);
        _gridBtn = TB("Cuadrícula", ToggleGrid, 104);
        _gridBtn.Selected = true;
        TB("▶ Probar en el equipo", TryOnDevice, 176, Ui.Ok, Ui.Cyan);
        TB("★ Agregar a mis temas", AddToLibrary, 184, Ui.Violet, Color.FromArgb(255, 40, 140));
        Controls.Add(top);

        _status = new Label
        {
            Dock = DockStyle.Bottom, Height = 26, ForeColor = Ui.Dim, BackColor = Ui.Panel, Font = Ui.Font(9),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
        };
        Controls.Add(_status);

        ResumeLayout(false);
        _layers.HandleCreated += (_, _) => DarkScroll(_layers);
        _props.HandleCreated += (_, _) => DarkScroll(_props);

        RefreshAll();
        SetStatus("Plantilla \"PIXDECK clásico\" cargada. Agrega elementos desde la izquierda o selecciona uno en el lienzo.");
        _timer.Tick += (_, _) => _canvas.Invalidate();
        _timer.Start();
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string app, string? idList);
    static void DarkScroll(Control c) { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }

    // ======================================================================
    // estado
    // ======================================================================
    public CustomTheme Theme => _theme;
    public int Selected => _sel;
    public bool Grid => _grid;

    public float? Value(int i)
    {
        var v = _hub.CurrentValues();
        if (i < v.Length && v[i] is float f) return f;
        return i switch
        {
            0 => 62, 1 => 34, 2 => 4850, 3 => 88, 4 => 55, 5 => 48, 6 => 1905, 7 => 210, 8 => 13.1f, 9 => 32,
            10 => 41, 11 => 2150, 12 => 8192, 13 => 1200, 14 => 45, 15 => 3.3f, 16 => 0, _ => null,
        };
    }
    public string NameText => HubService.Clean(_hub.Config.DeviceName, 16);
    public string CpuText => HubService.Clean(_hub.Config.CpuLabel, 16);
    public string GpuText => HubService.Clean(_hub.Config.GpuLabel, 16);
    public int Warn => _hub.Config.Warn;
    public int Alert => _hub.Config.AlertOn;

    public void SetStatus(string s) => _status.Text = s;

    void UpdateTitle() =>
        Text = $"PixDeck Hub · Creador de temas · {_theme.Name}{(_path != null ? "  (" + Path.GetFileName(_path) + ")" : "")}{(_dirty ? " *" : "")}";

    void RefreshAll()
    {
        if (_sel >= _theme.Widgets.Count) _sel = _theme.Widgets.Count - 1;
        RefreshLayers();
        BuildProps();
        UpdateTitle();
        _canvas.Invalidate();
    }

    public void Select(int i)
    {
        _sel = i;
        _suppress = true;
        _layers.SelectedIndex = i < 0 ? -1 : _theme.Widgets.Count - 1 - i;
        _suppress = false;
        BuildProps();
        _canvas.Invalidate();
    }

    // ======================================================================
    // deshacer / rehacer
    // ======================================================================
    string Snapshot() => JsonSerializer.Serialize(_theme);

    /// <summary>Guarda el estado antes de un cambio. Cambios seguidos del mismo campo se agrupan.</summary>
    public void PushUndo(string key)
    {
        var now = DateTime.Now;
        if (key == _lastUndoKey && (now - _lastUndoAt).TotalMilliseconds < 900) { _lastUndoAt = now; MarkDirty(); return; }
        _lastUndoKey = key;
        _lastUndoAt = now;
        _undo.Add(Snapshot());
        if (_undo.Count > 150) _undo.RemoveAt(0);
        _redo.Clear();
        MarkDirty();
    }

    void MarkDirty()
    {
        if (!_dirty) { _dirty = true; UpdateTitle(); }
    }

    void Undo()
    {
        if (_undo.Count == 0) { SetStatus("No hay nada para deshacer."); return; }
        _redo.Add(Snapshot());
        _theme = JsonSerializer.Deserialize<CustomTheme>(_undo[^1])!;
        _undo.RemoveAt(_undo.Count - 1);
        _lastUndoKey = "";
        _dirty = true;
        RefreshAll();
    }

    void Redo()
    {
        if (_redo.Count == 0) { SetStatus("No hay nada para rehacer."); return; }
        _undo.Add(Snapshot());
        _theme = JsonSerializer.Deserialize<CustomTheme>(_redo[^1])!;
        _redo.RemoveAt(_redo.Count - 1);
        _lastUndoKey = "";
        _dirty = true;
        RefreshAll();
    }

    // ======================================================================
    // elementos
    // ======================================================================
    void AddWidget(string type)
    {
        if (_theme.Widgets.Count >= CustomTheme.MaxWidgets) { SetStatus($"Máximo {CustomTheme.MaxWidgets} elementos por tema."); return; }
        PushUndo("add" + Guid.NewGuid());
        _theme.Widgets.Add(ThemeTemplates.NewWidget(type));
        _sel = _theme.Widgets.Count - 1;
        RefreshAll();
        Select(_sel);
        SetStatus($"Elemento agregado en el centro. Arrástralo a su lugar y ajusta sus propiedades a la derecha.");
    }

    void Duplicate()
    {
        if (_sel < 0 || _theme.Widgets.Count >= CustomTheme.MaxWidgets) return;
        PushUndo("dup" + Guid.NewGuid());
        var c = CustomTheme.CloneWidget(_theme.Widgets[_sel]);
        c.X += 10; c.Y += 10; c.X2 += 10; c.Y2 += 10;
        _theme.Widgets.Insert(_sel + 1, c);
        _sel++;
        RefreshAll();
        Select(_sel);
    }

    void DeleteSel()
    {
        if (_sel < 0) return;
        PushUndo("del" + Guid.NewGuid());
        _theme.Widgets.RemoveAt(_sel);
        _sel = Math.Min(_sel, _theme.Widgets.Count - 1);
        RefreshAll();
        Select(_sel);
    }

    void MoveLayer(int dir)
    {
        int j = _sel + dir;
        if (_sel < 0 || j < 0 || j >= _theme.Widgets.Count) return;
        PushUndo("layer" + Guid.NewGuid());
        (_theme.Widgets[_sel], _theme.Widgets[j]) = (_theme.Widgets[j], _theme.Widgets[_sel]);
        _sel = j;
        RefreshLayers();
        Select(_sel);
    }

    // ---------------- capas ----------------
    static readonly Dictionary<string, string> TypeNames = new()
    {
        ["rect"] = "▭ Rectángulo", ["circle"] = "● Círculo", ["line"] = "╱ Línea", ["text"] = "T Texto",
        ["value"] = "42 Valor", ["arc"] = "◔ Arco", ["bar"] = "▬ Barra", ["graph"] = "〰 Gráfica",
    };

    static string Describe(PxWidget w) => w.Type.ToLowerInvariant() switch
    {
        "text" => $"\"{w.Text}\"",
        "value" or "arc" or "bar" or "graph" => SourceName(w.Source),
        _ => $"{w.X},{w.Y}",
    };

    void RefreshLayers()
    {
        _suppress = true;
        _layers.BeginUpdate();
        _layers.Items.Clear();
        for (int i = _theme.Widgets.Count - 1; i >= 0; i--)
        {
            var w = _theme.Widgets[i];
            _layers.Items.Add($"{TypeNames.GetValueOrDefault(w.Type.ToLowerInvariant(), w.Type)}   {Describe(w)}");
        }
        _layers.EndUpdate();
        _layers.SelectedIndex = _sel < 0 ? -1 : _theme.Widgets.Count - 1 - _sel;
        _suppress = false;
    }

    void DrawLayer(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(sel ? Color.FromArgb(60, 40, 110) : Ui.Bg);
        e.Graphics.FillRectangle(bg, e.Bounds);
        int wi = _theme.Widgets.Count - 1 - e.Index;
        if (wi >= 0 && wi < _theme.Widgets.Count)
        {
            Color sw;
            try { sw = CustomTheme.ParseColor(_theme.Widgets[wi].Color); } catch { sw = Ui.Dim; }
            using var sb = new SolidBrush(sw);
            e.Graphics.FillRectangle(sb, e.Bounds.X + 4, e.Bounds.Y + 5, 10, e.Bounds.Height - 10);
        }
        TextRenderer.DrawText(e.Graphics, _layers.Items[e.Index]?.ToString(), _layers.Font,
            new Rectangle(e.Bounds.X + 20, e.Bounds.Y, e.Bounds.Width - 22, e.Bounds.Height),
            sel ? Color.White : Ui.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    // ======================================================================
    // propiedades
    // ======================================================================
    sealed record Item(string Key, string Text) { public override string ToString() => Text; }

    static readonly Dictionary<string, string> FieldNames = new()
    {
        ["X"] = "X", ["Y"] = "Y", ["W"] = "Ancho", ["H"] = "Alto", ["R"] = "Radio", ["X2"] = "X final", ["Y2"] = "Y final",
        ["Radius"] = "Esquinas", ["Width"] = "Grosor", ["Thickness"] = "Grosor", ["Start"] = "Ángulo inicio",
        ["End"] = "Ángulo fin", ["Color"] = "Color", ["Track"] = "Color de fondo", ["Text"] = "Texto", ["Font"] = "Fuente",
        ["Size"] = "Tamaño", ["Align"] = "Alineación", ["Source"] = "Dato", ["Decimals"] = "Decimales", ["Suffix"] = "Sufijo",
        ["TempColor"] = "Ámbar/rojo con calor", ["Min"] = "Mínimo", ["Max"] = "Máximo", ["Outline"] = "Solo borde", ["Fill"] = "Relleno",
    };

    static readonly Dictionary<string, string> SourceNames = new()
    {
        ["cpu_temp"] = "CPU · Temperatura", ["cpu_load"] = "CPU · Uso %", ["cpu_clock"] = "CPU · Frecuencia MHz",
        ["cpu_power"] = "CPU · Potencia W", ["gpu_temp"] = "GPU · Temperatura", ["gpu_load"] = "GPU · Uso %",
        ["gpu_clock"] = "GPU · Frecuencia MHz", ["gpu_power"] = "GPU · Potencia W", ["ram_used"] = "RAM · Usada GB",
        ["ram_total"] = "RAM · Total GB", ["ram_load"] = "RAM · Uso %", ["vram_used"] = "VRAM · Usada MB",
        ["vram_total"] = "VRAM · Total MB", ["vram_load"] = "VRAM · Uso %", ["vram_used_gb"] = "VRAM · Usada GB", ["vram_total_gb"] = "VRAM · Total GB", ["extra1"] = "Extra 1", ["extra2"] = "Extra 2",
        ["extra3"] = "Extra 3", ["extra4"] = "Extra 4",
    };
    static string SourceName(string s) => SourceNames.GetValueOrDefault(s.ToLowerInvariant(), s);

    static readonly Dictionary<string, string> AlignNames = new()
    {
        ["TL"] = "Arriba · izquierda", ["TC"] = "Arriba · centro", ["TR"] = "Arriba · derecha",
        ["ML"] = "Medio · izquierda", ["MC"] = "Medio · centro", ["MR"] = "Medio · derecha",
        ["BL"] = "Abajo · izquierda", ["BC"] = "Abajo · centro", ["BR"] = "Abajo · derecha",
    };

    readonly Dictionary<string, Control> _editors = new();

    void BuildProps()
    {
        _props.SuspendLayout();
        foreach (Control c in _props.Controls) c.Dispose();
        _props.Controls.Clear();
        _editors.Clear();
        int y = 10;

        void Row(string label, Control editor, int h = 28)
        {
            _props.Controls.Add(Ui.Label(label, 14, y + 4, Ui.Dim, Ui.Font(9)));
            editor.Left = 130; editor.Top = y; editor.Width = Math.Max(editor.Width, 60);
            _props.Controls.Add(editor);
            y += h + 6;
        }

        if (_sel < 0)
        {
            _props.Controls.Add(Ui.Label("TEMA", 14, y, Ui.Violet, Ui.Semi(11)));
            y += 32;
            Row("Nombre", ThemeText(_theme.Name, 16, v => _theme.Name = v));
            Row("Autor", ThemeText(_theme.Author, 30, v => _theme.Author = v));
            Row("Descripción", ThemeText(_theme.Description, 60, v => _theme.Description = v));
            Row("Fondo", ColorEditor(_theme.Background, v => _theme.Background = v, "bg"));
            y += 10;
            _props.Controls.Add(new Label
            {
                Left = 14, Top = y, Width = 290, Height = 150, ForeColor = Ui.Dim, Font = Ui.Font(8.5f),
                Text = "Selecciona un elemento en el lienzo o en las capas para editarlo.\n\n" +
                       "En los textos puedes usar:\n{name} nombre del equipo\n{cpu} {gpu} etiquetas\n{time} {hm} hora\n{status} ONLINE/OFFLINE",
            });
        }
        else
        {
            var w = _theme.Widgets[_sel];
            var type = w.Type.ToLowerInvariant();
            _props.Controls.Add(Ui.Label(TypeNames.GetValueOrDefault(type, type).ToUpperInvariant(), 14, y, Ui.Violet, Ui.Semi(11)));
            y += 32;
            var props = typeof(PxWidget).GetProperties().ToDictionary(p => p.Name);
            foreach (var f in CustomTheme.Fields[type])
            {
                var p = props[f];
                Control ed;
                if (f is "Color" or "Track") ed = ColorEditor((string)p.GetValue(w)!, v => p.SetValue(_theme.Widgets[_sel], v), f);
                else if (f == "Font")
                {
                    ed = Combo(CustomTheme.Fonts.Select(k => new Item(k, CustomTheme.FontLabel(k))), CustomTheme.CanonFont(w.Font),
                               v =>
                               {
                                   var sw = _theme.Widgets[_sel];
                                   sw.Font = v;
                                   sw.Size = Math.Clamp(sw.Size, 1, CustomTheme.MaxScale(v));
                                   BeginInvoke(BuildProps);   // el rango de "Tamaño" depende de la fuente
                               }, f);
                    ((ComboBox)ed).MaxDropDownItems = 20;
                    ((ComboBox)ed).DropDownWidth = 230;
                }
                else if (f == "Align") ed = Combo(CustomTheme.Aligns.Select(k => new Item(k, AlignNames[k])), w.Align.ToUpperInvariant(), v => _theme.Widgets[_sel].Align = v, f);
                else if (f == "Source")
                {
                    var list = type == "graph" ? CustomTheme.GraphSources : CustomTheme.Sources;
                    ed = Combo(list.Select(k => new Item(k, SourceName(k))), w.Source.ToLowerInvariant(), v => _theme.Widgets[_sel].Source = v, f);
                }
                else if (p.PropertyType == typeof(bool)) ed = Check((bool)p.GetValue(w)!, v => p.SetValue(_theme.Widgets[_sel], v), f);
                else if (p.PropertyType == typeof(string)) ed = TextEd((string)p.GetValue(w)!, f == "Text" ? 31 : 8, v => p.SetValue(_theme.Widgets[_sel], v), f);
                else if (p.PropertyType == typeof(float)) ed = Num((decimal)(float)p.GetValue(w)!, -100000, 100000, v => p.SetValue(_theme.Widgets[_sel], (float)v), f);
                else
                {
                    var (min, max) = f switch
                    {
                        "Size" => (1, CustomTheme.MaxScale(w.Font)), "Decimals" => (0, 2), "Start" or "End" => (0, 360), "Thickness" => (1, 80),
                        "Radius" => (0, 80), "Width" => (1, 20), "R" => (1, 200), "W" or "H" => (1, 400), _ => (-100, 420),
                    };
                    ed = Num((int)p.GetValue(w)!, min, max, v => p.SetValue(_theme.Widgets[_sel], (int)v), f);
                }
                _editors[f] = ed;
                Row(FieldNames.GetValueOrDefault(f, f), ed);
            }
            if (type is "arc")
                _props.Controls.Add(new Label { Left = 14, Top = y, Width = 290, Height = 40, ForeColor = Ui.Dim, Font = Ui.Font(8),
                    Text = "Ángulos: 0 = abajo, giran en sentido horario.\n60 → 300 deja la abertura abajo." });
        }
        Ui.Style(_props);
        _props.ResumeLayout();
    }

    /// <summary>Actualiza los editores sin reconstruirlos (mientras se arrastra en el lienzo).</summary>
    public void RefreshPropValues()
    {
        if (_sel < 0) return;
        var w = _theme.Widgets[_sel];
        _suppress = true;
        foreach (var (f, ed) in _editors)
            if (ed is NumericUpDown n)
            {
                var v = typeof(PxWidget).GetProperty(f)!.GetValue(w);
                decimal d = v is float fl ? (decimal)fl : Convert.ToDecimal(v);
                n.Value = Math.Clamp(d, n.Minimum, n.Maximum);
            }
        _suppress = false;
        RefreshLayers();
    }

    void Changed(string key, Action apply)
    {
        if (_suppress) return;
        PushUndo(key + _sel);
        apply();
        _canvas.Invalidate();
        if (key is "Text" or "Source" or "Color") RefreshLayers();
    }

    Control Num(decimal value, decimal min, decimal max, Action<decimal> set, string key)
    {
        var n = new NumericUpDown { Width = 90, Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) };
        n.ValueChanged += (_, _) => Changed(key, () => set(n.Value));
        return n;
    }

    Control TextEd(string value, int max, Action<string> set, string key)
    {
        var t = new TextBox { Width = 170, MaxLength = max, Text = value };
        t.TextChanged += (_, _) => Changed(key, () => set(t.Text));
        return t;
    }

    Control ThemeText(string value, int max, Action<string> set)
    {
        var t = new TextBox { Width = 170, MaxLength = max, Text = value };
        t.TextChanged += (_, _) => { if (_suppress) return; PushUndo("theme" + max); set(t.Text); UpdateTitle(); };
        return t;
    }

    Control Check(bool value, Action<bool> set, string key)
    {
        var c = new CheckBox { Width = 170, Checked = value, Text = "" };
        c.CheckedChanged += (_, _) => Changed(key, () => set(c.Checked));
        return c;
    }

    Control Combo(IEnumerable<Item> items, string current, Action<string> set, string key)
    {
        var cb = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var it in items) cb.Items.Add(it);
        cb.SelectedItem = cb.Items.Cast<Item>().FirstOrDefault(i => i.Key == current) ?? (cb.Items.Count > 0 ? cb.Items[0] : null);
        cb.SelectedIndexChanged += (_, _) => Changed(key, () => set(((Item)cb.SelectedItem!).Key));
        return cb;
    }

    Control ColorEditor(string value, Action<string> set, string key)
    {
        var p = new Panel { Width = 170, Height = 28, BackColor = Color.Transparent };
        var sw = new Button { Left = 0, Top = 0, Width = 40, Height = 26, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        sw.FlatAppearance.BorderColor = Ui.Border;
        var tb = new TextBox { Left = 48, Top = 1, Width = 90, MaxLength = 7, Text = value.ToUpperInvariant() };
        void Paint(string hex) { try { sw.BackColor = CustomTheme.ParseColor(hex); } catch { } }
        Paint(value);
        tb.TextChanged += (_, _) =>
        {
            var hex = tb.Text.Trim();
            if (!hex.StartsWith('#')) hex = "#" + hex;
            try { CustomTheme.ParseColor(hex); } catch { return; }
            Paint(hex);
            if (key == "bg") { if (!_suppress) { PushUndo("bg"); set(hex); _canvas.Invalidate(); } }
            else Changed(key, () => set(hex));
        };
        sw.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { FullOpen = true, Color = sw.BackColor };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                tb.Text = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
        };
        p.Controls.Add(sw);
        p.Controls.Add(tb);
        return p;
    }

    // ======================================================================
    // archivos
    // ======================================================================
    bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var r = MessageBox.Show(this, "Hay cambios sin guardar. ¿Guardarlos?", "Creador de temas",
                                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Cancel) return false;
        return r == DialogResult.No || SaveFile(false);
    }

    void ShowTemplates(Control anchor)
    {
        var menu = new ContextMenuStrip();
        for (int i = 0; i < ThemeTemplates.Names.Length; i++)
        {
            int idx = i;
            menu.Items.Add(ThemeTemplates.Names[i], null, (_, _) =>
            {
                if (!ConfirmDiscard()) return;
                LoadTheme(ThemeTemplates.Create(idx), null);
                SetStatus($"Nuevo tema desde la plantilla \"{ThemeTemplates.Names[idx]}\".");
            });
        }
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    void LoadTheme(CustomTheme t, string? path)
    {
        _theme = t;
        _path = path;
        _sel = -1;
        _undo.Clear();
        _redo.Clear();
        _dirty = false;
        RefreshAll();
    }

    void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        using var dlg = new OpenFileDialog
        {
            Title = "Abrir tema", InitialDirectory = DefaultFolder,
            Filter = "Temas PixDeck (*.pxtheme)|*.pxtheme|JSON (*.json)|*.json|Todos (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            LoadTheme(CustomTheme.Load(dlg.FileName), dlg.FileName);
            SetStatus($"Abierto: {dlg.FileName}");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            MessageBox.Show(this, ex.Message, "No se pudo abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    bool SaveFile(bool saveAs)
    {
        var problem = _theme.Problem();
        if (problem != null)
        {
            MessageBox.Show(this, problem, "El tema tiene un problema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        var path = _path;
        if (saveAs || path == null)
        {
            var safe = string.Concat(HubService.Clean(_theme.Name, 24).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            using var dlg = new SaveFileDialog
            {
                Title = "Guardar tema", InitialDirectory = DefaultFolder, FileName = safe.ToLowerInvariant() + ".pxtheme",
                Filter = "Temas PixDeck (*.pxtheme)|*.pxtheme",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            path = dlg.FileName;
        }
        try
        {
            _theme.Save(path);
            _path = path;
            _dirty = false;
            UpdateTitle();
            SetStatus($"Guardado: {path}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "No se pudo guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    // ======================================================================
    // equipo
    // ======================================================================
    void TryOnDevice()
    {
        var problem = _theme.Problem();
        if (problem != null) { MessageBox.Show(this, problem, "El tema tiene un problema", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!_hub.Link.Connected) { SetStatus("El equipo no está conectado por USB."); return; }
        var copy = _theme.Clone();
        SetStatus("Enviando el tema al equipo...");
        Task.Run(() => _hub.PreviewOnDevice(copy)).ContinueWith(t =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                _previewActive = t.Result;
                SetStatus(t.Result ? "✓ Mostrándose en el equipo (vista previa, no se guarda). Al cerrar el creador vuelve tu tema."
                                   : "No se pudo enviar al equipo.");
            });
        });
    }

    void AddToLibrary()
    {
        var problem = _theme.Problem();
        if (problem != null) { MessageBox.Show(this, problem, "El tema tiene un problema", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_path == null)
        {
            var safe = string.Concat(HubService.Clean(_theme.Name, 24).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            _path = Path.Combine(DefaultFolder, safe.ToLowerInvariant() + ".pxtheme");
        }
        if (!SaveFile(false)) return;
        try
        {
            int idx = _hub.ImportTheme(_path);
            _hub.SetTheme(idx);
            _previewActive = false;
            SetStatus($"★ \"{_hub.ThemeName(idx)}\" está en tus temas y se pondrá en el equipo en unos segundos.");
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            MessageBox.Show(this, ex.Message, "No se pudo agregar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void ToggleGrid()
    {
        _grid = !_grid;
        _gridBtn.Selected = _grid;
        _gridBtn.Invalidate();
        _canvas.Invalidate();
    }

    // ======================================================================
    // teclado
    // ======================================================================
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = ActiveControl is TextBox or NumericUpDown or ComboBox ||
                      (ActiveControl is ContainerControl cc && cc.ActiveControl is TextBox or NumericUpDown);
        switch (keyData)
        {
            case Keys.Control | Keys.Z: if (!typing) { Undo(); return true; } break;
            case Keys.Control | Keys.Y: if (!typing) { Redo(); return true; } break;
            case Keys.Control | Keys.S: SaveFile(false); return true;
            case Keys.Control | Keys.D: Duplicate(); return true;
            case Keys.Delete: if (!typing) { DeleteSel(); return true; } break;
            case Keys.Escape: if (!typing) { Select(-1); return true; } break;
        }
        if (!typing && _sel >= 0)
        {
            int step = (keyData & Keys.Shift) != 0 ? 10 : 1;
            int dx = 0, dy = 0;
            switch (keyData & ~Keys.Shift)
            {
                case Keys.Left: dx = -step; break;
                case Keys.Right: dx = step; break;
                case Keys.Up: dy = -step; break;
                case Keys.Down: dy = step; break;
            }
            if (dx != 0 || dy != 0)
            {
                PushUndo("arrows" + _sel);
                var w = _theme.Widgets[_sel];
                w.X += dx; w.Y += dy; w.X2 += dx; w.Y2 += dy;
                RefreshPropValues();
                _canvas.Invalidate();
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscard()) { e.Cancel = true; return; }
        if (_previewActive) _hub.EndPreview();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Lienzo del creador: dibuja el tema ampliado y maneja seleccionar / mover / redimensionar.</summary>
public sealed class EditorCanvas : Control
{
    readonly ThemeEditorForm _ed;
    readonly DateTime _t0 = DateTime.Now;
    float _z = 2, _ox, _oy;
    enum Drag { None, Move, Resize, Radius, P1, P2 }
    Drag _drag;
    PointF _start;
    PxWidget? _orig;

    public EditorCanvas(ThemeEditorForm ed)
    {
        _ed = ed;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Ui.Bg;
    }

    void Recalc()
    {
        _z = Math.Max(1, Math.Min((Width - 40) / 320f, (Height - 40) / 240f));
        _ox = (Width - 320 * _z) / 2;
        _oy = (Height - 240 * _z) / 2;
    }

    PointF ToTheme(Point p) => new((p.X - _ox) / _z, (p.Y - _oy) / _z);
    PointF ToScreen(float x, float y) => new(_ox + x * _z, _oy + y * _z);

    RectangleF BoundsOf(Graphics g, PxWidget w) =>
        CustomTheme.Bounds(w, g, _ed.Value, _ed.NameText, _ed.CpuText, _ed.GpuText);

    // asas (en coordenadas del tema)
    List<(Drag Kind, PointF P)> Handles(PxWidget w, RectangleF b)
    {
        var l = new List<(Drag, PointF)>();
        switch (w.Type.ToLowerInvariant())
        {
            case "rect": case "bar": case "graph": l.Add((Drag.Resize, new PointF(w.X + w.W, w.Y + w.H))); break;
            case "circle": case "arc": l.Add((Drag.Radius, new PointF(w.X + w.R, w.Y))); break;
            case "line": l.Add((Drag.P1, new PointF(w.X, w.Y))); l.Add((Drag.P2, new PointF(w.X2, w.Y2))); break;
        }
        return l;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Recalc();
        g.Clear(Ui.Bg);
        Ui.Hq(g);

        // marco de la pantalla
        var screen = new RectangleF(_ox, _oy, 320 * _z, 240 * _z);
        using (var bez = Ui.Round(RectangleF.Inflate(screen, 10, 10), 12))
        using (var bb = new SolidBrush(Color.FromArgb(4, 4, 8))) g.FillPath(bb, bez);

        var st = g.Save();
        g.SetClip(screen);
        g.TranslateTransform(_ox, _oy);
        g.ScaleTransform(_z, _z);
        float t = (float)(DateTime.Now - _t0).TotalSeconds;
        try { _ed.Theme.Render(g, _ed.Value, _ed.NameText, _ed.CpuText, _ed.GpuText, t, _ed.Warn, _ed.Alert); }
        catch { /* colores a medio escribir, etc. */ }
        g.Restore(st);

        if (_ed.Grid)
        {
            using var minor = new Pen(Color.FromArgb(22, 255, 255, 255));
            using var major = new Pen(Color.FromArgb(48, 255, 255, 255));
            for (int x = 10; x < 320; x += 10) g.DrawLine(x % 40 == 0 ? major : minor, ToScreen(x, 0), ToScreen(x, 240));
            for (int y = 10; y < 240; y += 10) g.DrawLine(y % 40 == 0 ? major : minor, ToScreen(0, y), ToScreen(320, y));
            using var center = new Pen(Color.FromArgb(70, Ui.Cyan)) { DashPattern = new[] { 4f, 4f } };
            g.DrawLine(center, ToScreen(160, 0), ToScreen(160, 240));
            g.DrawLine(center, ToScreen(0, 120), ToScreen(320, 120));
        }

        int sel = _ed.Selected;
        if (sel >= 0 && sel < _ed.Theme.Widgets.Count)
        {
            var w = _ed.Theme.Widgets[sel];
            var b = BoundsOf(g, w);
            var p0 = ToScreen(b.X, b.Y);
            using var pen = new Pen(Ui.Cyan, 1.5f) { DashPattern = new[] { 4f, 3f } };
            g.DrawRectangle(pen, p0.X - 2, p0.Y - 2, b.Width * _z + 4, b.Height * _z + 4);
            foreach (var (_, hp) in Handles(w, b))
            {
                var s = ToScreen(hp.X, hp.Y);
                g.FillRectangle(Brushes.White, s.X - 5, s.Y - 5, 10, 10);
                using var hpen = new Pen(Ui.Violet, 2);
                g.DrawRectangle(hpen, s.X - 5, s.Y - 5, 10, 10);
            }
        }
        using var border = new Pen(Ui.Border);
        g.DrawRectangle(border, screen.X, screen.Y, screen.Width, screen.Height);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        Recalc();
        var pt = ToTheme(e.Location);
        using var g = CreateGraphics();
        var widgets = _ed.Theme.Widgets;

        // 1) asas del elemento seleccionado
        int sel = _ed.Selected;
        if (sel >= 0 && sel < widgets.Count)
        {
            var w = widgets[sel];
            foreach (var (kind, hp) in Handles(w, BoundsOf(g, w)))
                if (Math.Abs(hp.X - pt.X) * _z <= 7 && Math.Abs(hp.Y - pt.Y) * _z <= 7)
                {
                    Begin(kind, pt, w);
                    return;
                }
        }
        // 2) elemento bajo el mouse (el de mas al frente primero)
        for (int i = widgets.Count - 1; i >= 0; i--)
        {
            if (!BoundsOf(g, widgets[i]).Contains(pt)) continue;
            if (i != sel) _ed.Select(i);
            Begin(Drag.Move, pt, widgets[i]);
            return;
        }
        _ed.Select(-1);
        base.OnMouseDown(e);
    }

    void Begin(Drag kind, PointF pt, PxWidget w)
    {
        _drag = kind;
        _start = pt;
        _orig = CustomTheme.CloneWidget(w);
        _ed.PushUndo("drag" + Guid.NewGuid());
    }

    int Snap(float v) => _ed.Grid ? (int)(Math.Round(v / 5f) * 5) : (int)Math.Round(v);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Recalc();
        var pt = ToTheme(e.Location);
        if (pt.X >= 0 && pt.Y >= 0 && pt.X <= 320 && pt.Y <= 240)
            _ed.SetStatus($"x: {(int)pt.X}   y: {(int)pt.Y}" + (_ed.Selected >= 0 ? "   ·   flechas = mover 1 px, Shift+flechas = 10 px" : ""));
        if (_drag == Drag.None || _orig == null || _ed.Selected < 0) return;

        var w = _ed.Theme.Widgets[_ed.Selected];
        float dx = pt.X - _start.X, dy = pt.Y - _start.Y;
        switch (_drag)
        {
            case Drag.Move:
                w.X = Snap(_orig.X + dx); w.Y = Snap(_orig.Y + dy);
                w.X2 = _orig.X2 + (w.X - _orig.X); w.Y2 = _orig.Y2 + (w.Y - _orig.Y);
                break;
            case Drag.Resize:
                w.W = Math.Max(2, Snap(_orig.W + dx)); w.H = Math.Max(2, Snap(_orig.H + dy));
                break;
            case Drag.Radius:
                w.R = Math.Max(2, Snap(_orig.R + dx));
                break;
            case Drag.P1: w.X = Snap(_orig.X + dx); w.Y = Snap(_orig.Y + dy); break;
            case Drag.P2: w.X2 = Snap(_orig.X2 + dx); w.Y2 = Snap(_orig.Y2 + dy); break;
        }
        _ed.RefreshPropValues();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _drag = Drag.None;
        _orig = null;
        base.OnMouseUp(e);
    }
}
