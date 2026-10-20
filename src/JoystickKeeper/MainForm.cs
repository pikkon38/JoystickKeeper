using Vortice.DirectInput;

namespace JoystickKeeper;

public sealed class MainForm : Form
{
    const int MaxSlots = BindingsFixer.MaxSlots;
    const string NoSlot = "Not used";

    readonly IDirectInput8 _di = DInput.DirectInput8Create();
    Profile _profile = Profile.Load();
    List<ScannedDevice> _scanned = new();
    readonly Dictionary<string, IDirectInputDevice8> _open = new();
    readonly Dictionary<string, string> _lastState = new();
    readonly Dictionary<string, DateTime> _lastActivity = new();
    bool _dirty;
    bool _loading;

    // How the bindings file is numbered right now (null = your saved order).
    AppliedFix? _applied;

    readonly DataGridView _grid = new();
    readonly ComboBox _scFile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 560 };
    readonly Label _status = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    readonly Label _launchState = new() { AutoSize = true, Padding = new Padding(10, 14, 0, 0), Font = new Font("Segoe UI", 10f) };
    readonly Button _apply = new() { Text = "Update bindings", AutoSize = true, MinimumSize = new Size(300, 44), Padding = new Padding(10, 0, 10, 0), Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
    readonly Button _restore = new() { Text = "Reset to saved order", AutoSize = true, Height = 44, Visible = false };
    readonly Button _openRsi = new() { Text = "Open RSI Launcher", AutoSize = true, Height = 44 };
    readonly Button _update = new() { AutoSize = true, Height = 44, Visible = false, BackColor = Color.FromArgb(198, 239, 206) };
    readonly LinkLabel _checkUpdates = new() { Text = "Check for updates", AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(0, 6, 0, 0) };
    UpdateInfo? _available;
    readonly Button _import = new() { Text = "Fill in from Star Citizen bindings", AutoSize = true };
    readonly System.Windows.Forms.Timer _poll = new() { Interval = 50 };
    readonly System.Windows.Forms.Timer _rescanSoon = new() { Interval = 1500 };

    public MainForm()
    {
        Text = $"Joystick Keeper v{Updater.Current.ToString(3)}";
        Width = 1180;
        Height = 600;
        MinimumSize = new Size(900, 450);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        BuildGrid();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 0), WrapContents = true };
        var rescan = new Button { Text = "Rescan", AutoSize = true };
        rescan.Click += (_, _) => Rescan();
        _import.Click += (_, _) => ImportFromStarCitizen(showSummary: true);
        var save = new Button { Text = "Save setup", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        save.Click += (_, _) => SaveProfile();
        top.Controls.AddRange(new Control[] { rescan, _import, save,
            new Label { Text = "Bindings file:", AutoSize = true, Padding = new Padding(12, 6, 0, 0) }, _scFile });

        var help = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            UseMnemonic = false,
            Height = 46,
            Padding = new Padding(10, 6, 10, 0),
            Text = "Move a stick or press a button and its row lights up green. \"Your slot\" is the js number your bindings were made with. " +
                   "Pick a slot for each device and click Save setup. Whenever your devices were plugged in differently, click Update bindings before starting the game.",
        };

        var launchBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(8, 8, 8, 0) };
        _apply.Click += (_, _) => UpdateBindings();
        _restore.Click += (_, _) => ResetBindings("Bindings put back to your saved order.");
        _openRsi.Click += (_, _) => OpenRsiLauncher();
        _update.Click += async (_, _) => await InstallUpdate();
        _checkUpdates.LinkClicked += async (_, _) => await CheckForUpdates(userAsked: true);
        launchBar.Controls.AddRange(new Control[] { _apply, _restore, _openRsi, _update, _launchState });

        // Grow the help line to fit its text at any window width or display scaling.
        void FitHelp() => help.Height = TextRenderer.MeasureText(help.Text, help.Font,
            new Size(Math.Max(100, help.ClientSize.Width - help.Padding.Horizontal), 0), TextFormatFlags.WordBreak).Height + help.Padding.Vertical + 4;
        help.Resize += (_, _) => FitHelp();
        help.FontChanged += (_, _) => FitHelp();

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(10, 0, 10, 0) };
        bottom.Controls.Add(_status);
        bottom.Controls.Add(_checkUpdates);

        Controls.Add(_grid);
        Controls.Add(help);
        Controls.Add(top);
        Controls.Add(launchBar);
        Controls.Add(bottom);

        _poll.Tick += (_, _) => PollActivity();
        _rescanSoon.Tick += (_, _) => { _rescanSoon.Stop(); Rescan(); };

        Load += (_, _) => OnFirstLoad();
        FormClosing += OnClosing;
    }

    void OnFirstLoad()
    {
        var files = StarCitizen.FindActionMaps();
        if (_profile.ActionMapsPath != null && File.Exists(_profile.ActionMapsPath) &&
            !files.Contains(_profile.ActionMapsPath, StringComparer.OrdinalIgnoreCase))
            files.Insert(0, _profile.ActionMapsPath);
        foreach (var f in files) _scFile.Items.Add(f);
        int saved = _profile.ActionMapsPath == null ? -1 : _scFile.Items.IndexOf(_profile.ActionMapsPath);
        if (_scFile.Items.Count > 0) _scFile.SelectedIndex = Math.Max(0, saved);

        _applied = AppliedFix.Load();
        Rescan();
        UpdateLaunchUi();
        _poll.Start();
        _ = CheckForUpdates(userAsked: false);
    }

    void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Now", HeaderText = "Game sees it as", ReadOnly = true, FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Device", HeaderText = "Device", ReadOnly = true, FillWeight = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "VID:PID", ReadOnly = true, FillWeight = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = "Type", ReadOnly = true, FillWeight = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Your name for it", FillWeight = 100 });
        var slot = new DataGridViewComboBoxColumn { Name = "Slot", HeaderText = "Your slot", FillWeight = 55, FlatStyle = FlatStyle.Flat };
        slot.Items.Add(NoSlot);
        for (int i = 1; i <= MaxSlots; i++) slot.Items.Add($"js{i}");
        _grid.Columns.Add(slot);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "State", HeaderText = "Status", ReadOnly = true, FillWeight = 140 });

        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || _loading) return;
            _dirty = true;
            UpdateRowState(_grid.Rows[e.RowIndex]);
            UpdateStatusBar();
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.DataError += (_, e) => e.Cancel = true;
    }

    string? SelectedActionMaps() => _scFile.SelectedItem is string s && File.Exists(s) ? s : null;

    // ---------- scanning ----------

    void Rescan()
    {
        // Keep unsaved edits across a rescan.
        var edits = _grid.Rows.Cast<DataGridViewRow>()
            .ToDictionary(r => (string)r.Tag!, r => ((string?)r.Cells["Name"].Value, (string?)r.Cells["Slot"].Value));

        _loading = true;
        CloseDevices();
        try { _scanned = DeviceScanner.Scan(_di); }
        catch (Exception ex) { _scanned = new(); MessageBox.Show(this, "Could not list game controllers:\n" + ex.Message, Text); }

        _grid.Rows.Clear();
        foreach (var d in _scanned)
        {
            var saved = _profile.Find(d.Key);
            int i = _grid.Rows.Add(d.GameSlot > 0 ? $"js{d.GameSlot}" : "Gamepad (no js slot)", d.ProductName, d.VidPid, d.Kind,
                saved?.FriendlyName ?? "", SlotText(saved?.StarCitizenSlot ?? 0), "");
            _grid.Rows[i].Tag = d.Key;
            if (d.GameSlot == 0) _grid.Rows[i].DefaultCellStyle.ForeColor = SystemColors.GrayText;
            OpenDevice(d);
        }
        // Devices in the saved setup that aren't plugged in.
        foreach (var p in _profile.Devices.Where(p => _scanned.All(s => s.Key != p.Key)))
        {
            int i = _grid.Rows.Add("-", p.ProductName, $"{p.VendorId:X4}:{p.ProductId:X4}", "", p.FriendlyName, SlotText(p.StarCitizenSlot), "");
            _grid.Rows[i].Tag = p.Key;
        }
        foreach (DataGridViewRow r in _grid.Rows)
        {
            if (edits.TryGetValue((string)r.Tag!, out var e))
            {
                r.Cells["Name"].Value = e.Item1;
                r.Cells["Slot"].Value = e.Item2 ?? NoSlot;
            }
            UpdateRowState(r);
        }
        _loading = false;
        UpdateStatusBar();
    }

    static string SlotText(int slot) => slot is >= 1 and <= MaxSlots ? $"js{slot}" : NoSlot;

    static int SlotNumber(object? v) =>
        v is string s && s.StartsWith("js") && int.TryParse(s[2..], out var n) ? n : 0;

    void UpdateRowState(DataGridViewRow r)
    {
        var key = (string)r.Tag!;
        var dev = _scanned.FirstOrDefault(s => s.Key == key);
        int want = SlotNumber(r.Cells["Slot"].Value);
        string text;
        Color color;
        if (dev == null) { text = want > 0 ? "Not plugged in" : "Not plugged in (not used)"; color = want > 0 ? Color.Firebrick : SystemColors.GrayText; }
        else if (dev.GameSlot == 0) { text = want > 0 ? "Game treats this as a gamepad, it can't have a js slot" : "Ignored by the game's joystick list"; color = want > 0 ? Color.Firebrick : SystemColors.GrayText; }
        else if (want == 0) { text = "Connected, not used"; color = SystemColors.GrayText; }
        else if (FileSlot(want) == dev.GameSlot)
        { text = want == dev.GameSlot ? "Bindings match" : $"Bindings match (moved to js{dev.GameSlot})"; color = Color.ForestGreen; }
        else { text = $"Windows gave it js{dev.GameSlot}, click Update bindings"; color = Color.DarkOrange; }
        r.Cells["State"].Value = text;
        r.Cells["State"].Style.ForeColor = color;
    }

    /// <summary>The js number the bindings file uses right now for a home slot.</summary>
    int FileSlot(int homeSlot) => _applied != null && _applied.Map.TryGetValue(homeSlot, out var n) ? n : homeSlot;

    void UpdateStatusBar()
    {
        var dupes = _grid.Rows.Cast<DataGridViewRow>()
            .Select(r => SlotNumber(r.Cells["Slot"].Value)).Where(n => n > 0)
            .GroupBy(n => n).Where(g => g.Count() > 1).Select(g => $"js{g.Key}").ToList();
        var parts = new List<string> { $"{_scanned.Count(s => s.GameSlot > 0)} joysticks found" };
        if (dupes.Count > 0) parts.Add("Two devices share " + string.Join(", ", dupes));
        parts.Add(_dirty ? "Unsaved changes" :
            File.Exists(Profile.FilePath) ? $"Setup saved {_profile.SavedAt:g}" : "No setup saved yet");
        _status.Text = string.Join("   ·   ", parts);
        _status.ForeColor = dupes.Count > 0 ? Color.Firebrick : SystemColors.ControlText;
    }

    // ---------- live input, so you can tell which physical stick is which ----------

    void OpenDevice(ScannedDevice d)
    {
        try
        {
            var dev = _di.CreateDevice(d.InstanceGuid);
            dev.SetCooperativeLevel(Handle, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
            dev.SetDataFormat<RawJoystickState>();
            dev.Acquire();
            _open[d.Key] = dev;
        }
        catch { /* some devices refuse to open; they just won't light up */ }
    }

    void CloseDevices()
    {
        foreach (var d in _open.Values)
        {
            try { d.Unacquire(); } catch { }
            d.Dispose();
        }
        _open.Clear();
        _lastState.Clear();
    }

    void PollActivity()
    {
        if (WindowState == FormWindowState.Minimized) return;
        var now = DateTime.Now;
        foreach (var (key, dev) in _open)
        {
            string sig;
            try
            {
                dev.Poll();
                var s = dev.GetCurrentJoystickState();
                // Coarse axis buckets so sensor jitter doesn't count as movement.
                static int B(int v) => v / 4096;
                sig = string.Join(",", B(s.X), B(s.Y), B(s.Z), B(s.RotationX), B(s.RotationY), B(s.RotationZ),
                    string.Join("", s.Sliders.Select(B)), string.Join("", s.PointOfViewControllers),
                    new string(s.Buttons.Select(b => b ? '1' : '0').ToArray()));
            }
            catch { continue; }
            if (_lastState.TryGetValue(key, out var prev) && prev != sig) _lastActivity[key] = now;
            _lastState[key] = sig;
        }
        foreach (DataGridViewRow r in _grid.Rows)
        {
            bool active = _lastActivity.TryGetValue((string)r.Tag!, out var t) && (now - t).TotalMilliseconds < 1200;
            var bg = active ? Color.FromArgb(198, 239, 206) : SystemColors.Window;
            if (r.DefaultCellStyle.BackColor != bg) r.DefaultCellStyle.BackColor = bg;
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_DEVICECHANGE = 0x0219;
        if (m.Msg == WM_DEVICECHANGE) { _rescanSoon.Stop(); _rescanSoon.Start(); }
        base.WndProc(ref m);
    }

    // ---------- Star Citizen import / save ----------

    void ImportFromStarCitizen(bool showSummary)
    {
        var path = SelectedActionMaps();
        if (path == null)
        {
            if (showSummary) MessageBox.Show(this, "No Star Citizen bindings file was found. Start the game once, save your controls, then try again.", Text);
            return;
        }
        List<ScJoystick> sc;
        try { sc = StarCitizen.ReadJoysticks(path); }
        catch (Exception ex) { MessageBox.Show(this, "Could not read the bindings file:\n" + ex.Message, Text); return; }

        var used = new HashSet<DataGridViewRow>();
        var lines = new List<string>();
        foreach (var j in sc.Where(j => j.VendorId != 0 || j.ProductId != 0))
        {
            var row = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => !used.Contains(r) &&
                ((string)r.Cells["Id"].Value).Equals($"{j.VendorId:X4}:{j.ProductId:X4}", StringComparison.OrdinalIgnoreCase));
            if (row == null) { lines.Add($"js{j.Instance}: {j.ProductName} (not plugged in, skipped)"); continue; }
            used.Add(row);
            // The file may be renumbered for today's order; slots are kept in your saved numbering.
            int home = HomeSlot(j.Instance);
            row.Cells["Slot"].Value = SlotText(home);
            if (string.IsNullOrWhiteSpace(row.Cells["Name"].Value as string)) row.Cells["Name"].Value = j.ProductName;
            lines.Add($"js{home}: {j.ProductName}");
        }
        _dirty = true;
        UpdateStatusBar();
        if (showSummary)
            MessageBox.Show(this, "Your bindings expect:\n\n" + (lines.Count > 0 ? string.Join("\n", lines) : "(no joysticks listed)") +
                "\n\nCheck the slots look right, then click Save setup.", Text);
    }

    int HomeSlot(int fileSlot) => _applied?.HomeSlot(fileSlot) ?? fileSlot;

    bool SaveProfile()
    {
        _grid.EndEdit();
        var p = new Profile { RsiLauncherPath = _profile.RsiLauncherPath, ActionMapsPath = SelectedActionMaps() ?? _profile.ActionMapsPath };
        foreach (DataGridViewRow r in _grid.Rows)
        {
            var id = ((string)r.Cells["Id"].Value).Split(':');
            p.Devices.Add(new ProfileDevice
            {
                Key = (string)r.Tag!,
                FriendlyName = (r.Cells["Name"].Value as string ?? "").Trim(),
                ProductName = (string)r.Cells["Device"].Value,
                VendorId = Convert.ToInt32(id[0], 16),
                ProductId = Convert.ToInt32(id[1], 16),
                StarCitizenSlot = SlotNumber(r.Cells["Slot"].Value),
            });
        }
        var dupes = p.Devices.Where(d => d.StarCitizenSlot > 0).GroupBy(d => d.StarCitizenSlot).Where(g => g.Count() > 1).ToList();
        if (dupes.Count > 0)
        {
            MessageBox.Show(this, "Two devices can't share a slot: " + string.Join(", ", dupes.Select(g => $"js{g.Key}")), Text);
            return false;
        }
        // New slots mean a new saved numbering, so put the file back in the old one before switching.
        bool slotsChanged = p.Devices.Any(d => (_profile.Find(d.Key)?.StarCitizenSlot ?? 0) != d.StarCitizenSlot) ||
                            _profile.Devices.Any(d => d.StarCitizenSlot > 0 && p.Find(d.Key) == null);
        if (_applied != null && slotsChanged && !ResetBindings("Bindings put back to your saved order before saving the new slots.")) return false;
        try { p.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, Text); return false; }
        _profile = p;
        _dirty = false;
        UpdateStatusBar();
        return true;
    }

    // ---------- update bindings ----------

    void UpdateBindings()
    {
        if (_dirty)
        {
            if (MessageBox.Show(this, "Save your setup first?", Text, MessageBoxButtons.OKCancel) != DialogResult.OK || !SaveProfile()) return;
        }
        if (_profile.Devices.All(d => d.StarCitizenSlot == 0))
        {
            MessageBox.Show(this, "Pick a slot for at least one device (or click Fill in from Star Citizen bindings) and save first.", Text);
            return;
        }
        if (GameLauncher.IsGameRunning())
        {
            MessageBox.Show(this, "Close Star Citizen first. It rewrites the bindings file when it quits, which would undo the update.", Text);
            return;
        }
        var path = SelectedActionMaps();
        if (path == null)
        {
            MessageBox.Show(this, "No Star Citizen bindings file was found, so there's nothing to update. Start the game once and save your controls.", Text);
            return;
        }

        Rescan();
        var (map, missing) = BindingsFixer.ComputeMap(_profile, _scanned);
        if (missing.Count > 0 &&
            MessageBox.Show(this, "Not plugged in: " + string.Join(", ", missing) + "\n\nUpdate the others anyway?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        var mismatch = CheckFileMatchesProfile(path);
        if (mismatch != null &&
            MessageBox.Show(this, mismatch + "\n\nIf you rebound your controls since saving, click Fill in from Star Citizen bindings and save again.\n\nUpdate anyway?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (_profile.ActionMapsPath != path)
        {
            _profile.ActionMapsPath = path;
            try { _profile.Save(); } catch { }
        }

        try { _applied = AppliedFix.Update(path, _applied, map); }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Windows won't let Joystick Keeper change the bindings file. Right-click Joystick Keeper and choose Run as administrator, then try again.", Text);
            return;
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not update the bindings file:\n" + ex.Message, Text); return; }

        Rescan();
        UpdateLaunchUi(_applied == null ? "Bindings match your devices. Nothing needed moving." : $"Bindings updated for today's order: {Moves()}.");
    }

    void OpenRsiLauncher()
    {
        var exe = GameLauncher.FindRsiLauncher(_profile.RsiLauncherPath);
        if (exe == null)
        {
            using var dlg = new OpenFileDialog { Title = "Find RSI Launcher.exe", Filter = "RSI Launcher|RSI Launcher.exe|Programs|*.exe" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            exe = dlg.FileName;
        }
        if (_profile.RsiLauncherPath != exe)
        {
            _profile.RsiLauncherPath = exe;
            try { _profile.Save(); } catch { }
        }
        try { GameLauncher.StartRsiLauncher(exe); }
        catch (Exception ex) { MessageBox.Show(this, "Could not start the RSI Launcher:\n" + ex.Message, Text); }
    }

    /// <summary>Checks that the bindings file still lists the same devices in the same slots as the saved setup.</summary>
    string? CheckFileMatchesProfile(string path)
    {
        try
        {
            var inFile = StarCitizen.ReadJoysticks(path).Where(j => j.VendorId != 0 || j.ProductId != 0).ToList();
            foreach (var p in _profile.Devices.Where(d => d.StarCitizenSlot > 0))
            {
                var j = inFile.FirstOrDefault(x => HomeSlot(x.Instance) == p.StarCitizenSlot);
                if (j != null && (j.VendorId != p.VendorId || j.ProductId != p.ProductId))
                    return $"Your saved setup has {Label(p)} on js{p.StarCitizenSlot}, but the bindings file now lists {j.ProductName} there.";
            }
        }
        catch { /* unreadable: the fix itself will report the problem */ }
        return null;

        static string Label(ProfileDevice d) => string.IsNullOrEmpty(d.FriendlyName) ? d.ProductName : d.FriendlyName;
    }

    bool ResetBindings(string message)
    {
        if (_applied == null) return true;
        if (GameLauncher.IsGameRunning())
        {
            MessageBox.Show(this, "Close Star Citizen first. It rewrites the bindings file when it quits, which would undo the change.", Text);
            return false;
        }
        try { _applied.Restore(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not put the bindings back:\n" + ex.Message + $"\n\nBackups are in {AppliedFix.BackupFolder}", Text);
            return false;
        }
        _applied = null;
        Rescan();
        UpdateLaunchUi(message);
        return true;
    }

    // ---------- updates ----------

    async Task CheckForUpdates(bool userAsked)
    {
        _checkUpdates.Enabled = false;
        try
        {
            _available = await Updater.CheckAsync();
            if (_available != null)
            {
                _update.Text = $"Update to v{_available.Version.ToString(3)}";
                _update.Visible = true;
            }
            else if (userAsked)
                MessageBox.Show(this, $"You have the latest version (v{Updater.Current.ToString(3)}).", Text);
        }
        catch (Exception ex)
        {
            if (userAsked) MessageBox.Show(this, "Couldn't check for updates:\n" + ex.Message, Text);
        }
        finally { _checkUpdates.Enabled = true; }
    }

    async Task InstallUpdate()
    {
        if (_available == null) return;
        if (_dirty && !SaveProfile()) return;
        _update.Enabled = false;
        _update.Text = "Downloading...";
        try
        {
            await Updater.DownloadAndStageAsync(_available);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Windows won't let Joystick Keeper replace itself in this folder. Move it to your Desktop or Documents, or run it as administrator.", Text);
            _update.Enabled = true; _update.Text = $"Update to v{_available.Version.ToString(3)}";
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The update didn't download:\n" + ex.Message, Text);
            _update.Enabled = true; _update.Text = $"Update to v{_available.Version.ToString(3)}";
            return;
        }
        _dirty = false;
        Close(); // The update script waits for this process to exit, swaps the exe, and reopens it.
    }

    void UpdateLaunchUi(string? message = null)
    {
        _restore.Visible = _applied != null;
        if (message != null) _launchState.Text = message;
        else _launchState.Text = _applied == null ? "Bindings are in your saved order." : $"Bindings are set for: {Moves()}.";
    }

    string Moves() => _applied == null ? "" : string.Join(", ", _applied.Map
        .Where(kv => kv.Key != kv.Value && _profile.Devices.Any(d => d.StarCitizenSlot == kv.Key))
        .OrderBy(kv => kv.Key).Select(kv => $"js{kv.Key}→js{kv.Value}"));

    void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_dirty)
        {
            var a = MessageBox.Show(this, "Save your setup before closing?", Text, MessageBoxButtons.YesNoCancel);
            if (a == DialogResult.Cancel) { e.Cancel = true; return; }
            if (a == DialogResult.Yes && !SaveProfile()) { e.Cancel = true; return; }
        }
        _poll.Stop();
        CloseDevices();
        _di.Dispose();
    }
}
