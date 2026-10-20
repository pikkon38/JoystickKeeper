using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace JoystickKeeper;

/// <summary>Dark look shared with ShakerRouter: same palette, card panels, flat buttons.</summary>
static class Theme
{
    public static readonly Color Bg = ColorTranslator.FromHtml("#0F1013");
    public static readonly Color Surface = ColorTranslator.FromHtml("#17191E");
    public static readonly Color Raised = ColorTranslator.FromHtml("#1F2228");
    public static readonly Color Line = ColorTranslator.FromHtml("#2B2F37");
    public static readonly Color Accent = ColorTranslator.FromHtml("#FF7A1A");
    public static readonly Color Good = ColorTranslator.FromHtml("#38D39F");
    public static readonly Color Text = ColorTranslator.FromHtml("#ECEEF2");
    public static readonly Color Muted = ColorTranslator.FromHtml("#8A909C");
    public static readonly Color Warn = ColorTranslator.FromHtml("#F5B83D");
    public static readonly Color Bad = ColorTranslator.FromHtml("#FF5C5C");
    /// <summary>Row highlight while a device is being moved.</summary>
    public static readonly Color Active = ColorTranslator.FromHtml("#1C3B31");

    public static Font Ui(float size = 10f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font SemiBold(float size) => new("Segoe UI Semibold", size);

    public static Label Caption(string text) => new()
    {
        Text = text.ToUpperInvariant(), AutoSize = true, ForeColor = Muted, Font = SemiBold(8.5f), Margin = new Padding(0, 0, 0, 6),
    };

    public static Button Button(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseMnemonic = false,
            Padding = new Padding(12, 4, 12, 4), MinimumSize = new Size(0, 36), Margin = new Padding(0, 0, 8, 0),
            BackColor = primary ? Accent : Raised, ForeColor = primary ? Bg : Text,
            Font = primary ? SemiBold(10.5f) : Ui(9.5f),
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Line;
        b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(Accent, 0.2f) : Line;
        b.FlatAppearance.MouseDownBackColor = primary ? ControlPaint.Dark(Accent, 0.1f) : Surface;
        return b;
    }

    /// <summary>A dark popup menu (the system drop-down lists can't be fully themed).</summary>
    public static ContextMenuStrip Menu() => new()
    {
        Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false },
        BackColor = Raised, ForeColor = Text, Font = Ui(10f), ShowImageMargin = false, ShowCheckMargin = false,
    };

    /// <summary>Draws a small down chevron centred in <paramref name="r"/>.</summary>
    public static void Chevron(Graphics g, Rectangle r, Color color)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        using var pen = new Pen(color, 1.6f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLines(pen, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
    }

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Raised;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => Line;
        public override Color MenuItemSelected => Line;
        public override Color MenuItemSelectedGradientBegin => Line;
        public override Color MenuItemSelectedGradientEnd => Line;
        public override Color ImageMarginGradientBegin => Raised;
        public override Color ImageMarginGradientMiddle => Raised;
        public override Color ImageMarginGradientEnd => Raised;
        public override Color CheckBackground => Raised;
        public override Color CheckSelectedBackground => Line;
        public override Color CheckPressedBackground => Line;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BorderStyle = BorderStyle.None;
        g.BackgroundColor = Surface;
        g.GridColor = Line;
        g.EnableHeadersVisualStyles = false;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface, ForeColor = Muted, SelectionBackColor = Surface, SelectionForeColor = Muted,
            Font = SemiBold(8.5f), Padding = new Padding(6, 8, 6, 8), WrapMode = DataGridViewTriState.True,
        };
        g.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface, ForeColor = Text, SelectionBackColor = Raised, SelectionForeColor = Text,
            Font = Ui(10f), Padding = new Padding(6, 0, 6, 0),
        };
        g.RowTemplate.Height = 38;
        // Dark dropdown inside the slot cells.
        g.EditingControlShowing += (_, e) =>
        {
            if (e.Control is ComboBox c) { c.BackColor = Raised; c.ForeColor = Text; c.FlatStyle = FlatStyle.Flat; }
            else if (e.Control is TextBox t) { t.BackColor = Raised; t.ForeColor = Text; t.BorderStyle = BorderStyle.None; }
        };
    }

    /// <summary>Dark window title bar on Windows 10 20H1+ and 11.</summary>
    public static void DarkTitleBar(Form f)
    {
        try { int on = 1; DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int)); } catch { }
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>A rounded panel with a caption, like ShakerRouter's cards.</summary>
sealed class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Padding = new Padding(16, 14, 16, 14);
        Margin = new Padding(0, 0, 0, 12);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Rounded(r, 8);
        using var fill = new SolidBrush(BackColor);
        using var pen = new Pen(Theme.Line);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }

    static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>A dark drop-down list: shows the selected item with a chevron and opens a dark menu on click.</summary>
sealed class DarkDropDown : Control
{
    public List<string> Items { get; } = new();
    int _selected = -1;
    bool _hover;

    public event EventHandler? SelectedIndexChanged;

    public DarkDropDown()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Raised;
        ForeColor = Theme.Text;
        Font = Theme.Ui(9.5f);
        Cursor = Cursors.Hand;
        Height = 36;
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem => _selected >= 0 && _selected < Items.Count ? Items[_selected] : null;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Items.Count == 0) return;
        var menu = Theme.Menu();
        for (int i = 0; i < Items.Count; i++)
        {
            int index = i;
            var item = new ToolStripMenuItem(Items[i]) { ForeColor = i == _selected ? Theme.Accent : Theme.Text };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_hover ? Theme.Line : BackColor);
        using (var pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        var text = SelectedItem ?? "No file found";
        TextRenderer.DrawText(g, text, Font, new Rectangle(10, 0, Width - 40, Height),
            SelectedItem == null ? Theme.Muted : ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine);
        Theme.Chevron(g, new Rectangle(Width - 30, 0, 24, Height), Theme.Muted);
    }
}
