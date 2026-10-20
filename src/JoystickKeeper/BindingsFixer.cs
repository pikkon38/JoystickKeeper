using System.Text.Json;
using System.Text.RegularExpressions;

namespace JoystickKeeper;

/// <summary>
/// Renumbers the js slots in Star Citizen's actionmaps.xml.
///
/// Your saved setup says which js number each device's bindings were made with (the "home" numbering). Update bindings
/// remaps the file home -> today's Windows order and remembers that mapping, so the next update can work out the
/// file's current numbering, keeping anything rebound in-game, and "Reset" can put it back to the home numbering.
/// </summary>
public static class BindingsFixer
{
    public const int MaxSlots = 8;

    // input="js4_button5", input="js1_ " (cleared binding) ...
    static readonly Regex JsInput = new(@"(\binput\s*=\s*"")js(\d+)_", RegexOptions.IgnoreCase);
    // <options type="joystick" instance="3" ...
    static readonly Regex JsOptions = new(@"(<options\s+type\s*=\s*""joystick""\s+instance\s*=\s*"")(\d+)("")", RegexOptions.IgnoreCase);

    /// <summary>Applies a slot mapping (old js number -> new js number) to the whole file. Unmapped numbers are left alone.</summary>
    public static string Remap(string text, IReadOnlyDictionary<int, int> map)
    {
        // One pass each, so js1->js2 and js2->js1 swaps don't chain into each other.
        text = JsInput.Replace(text, m =>
            map.TryGetValue(int.Parse(m.Groups[2].Value), out var to) ? $"{m.Groups[1].Value}js{to}_" : m.Value);
        text = JsOptions.Replace(text, m =>
            map.TryGetValue(int.Parse(m.Groups[2].Value), out var to) ? $"{m.Groups[1].Value}{to}{m.Groups[3].Value}" : m.Value);
        return SortJoystickOptions(text);
    }

    /// <summary>Keeps the &lt;options type="joystick"&gt; blocks in instance order after a remap, which is how the game writes them.</summary>
    static string SortJoystickOptions(string text)
    {
        var block = new Regex(@"[ \t]*<options\s+type\s*=\s*""joystick""\s+instance\s*=\s*""(\d+)""(?:[^>]*/>|[^>]*>.*?</options>)\r?\n?",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = block.Matches(text).Cast<Match>().ToList();
        if (matches.Count < 2) return text;
        // Only reorder when the blocks are one contiguous run (the normal layout); otherwise leave the file as is.
        for (int i = 1; i < matches.Count; i++)
            if (matches[i].Index != matches[i - 1].Index + matches[i - 1].Length) return text;
        var sorted = matches.OrderBy(m => int.Parse(m.Groups[1].Value)).Select(m => m.Value);
        int start = matches[0].Index, end = matches[^1].Index + matches[^1].Length;
        return text[..start] + string.Concat(sorted) + text[end..];
    }

    /// <summary>Inverse of a mapping, used to put the file back after the game closes.</summary>
    public static Dictionary<int, int> Invert(IReadOnlyDictionary<int, int> map) => map.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>
    /// Works out home slot -> current slot for every js number. Devices that are plugged in go to the slot the game
    /// will give them; the leftover home slots fill the leftover numbers in order, so the mapping is always a clean swap.
    /// </summary>
    public static (Dictionary<int, int> map, List<string> missing) ComputeMap(Profile profile, IReadOnlyList<ScannedDevice> scanned)
    {
        var map = new Dictionary<int, int>();
        var missing = new List<string>();
        foreach (var p in profile.Devices.Where(d => d.StarCitizenSlot > 0))
        {
            var dev = scanned.FirstOrDefault(s => s.Key == p.Key && s.GameSlot > 0);
            if (dev == null) { missing.Add(string.IsNullOrEmpty(p.FriendlyName) ? p.ProductName : p.FriendlyName); continue; }
            map[p.StarCitizenSlot] = dev.GameSlot;
        }
        int top = Math.Max(MaxSlots, Math.Max(map.Keys.DefaultIfEmpty(0).Max(), map.Values.DefaultIfEmpty(0).Max()));
        var freeTargets = new Queue<int>(Enumerable.Range(1, top).Where(n => !map.ContainsValue(n)));
        foreach (var from in Enumerable.Range(1, top).Where(n => !map.ContainsKey(n)))
            map[from] = freeTargets.Dequeue();
        return (map, missing);
    }

    /// <summary>First <paramref name="a"/> then <paramref name="b"/>: x -> a[x] -> b[a[x]].</summary>
    public static Dictionary<int, int> Compose(IReadOnlyDictionary<int, int> a, IReadOnlyDictionary<int, int> b) =>
        a.ToDictionary(kv => kv.Key, kv => b.TryGetValue(kv.Value, out var z) ? z : kv.Value);

    public static bool IsIdentity(IReadOnlyDictionary<int, int> map) => map.All(kv => kv.Key == kv.Value);
}

/// <summary>Remembers how the bindings file is currently numbered (home slot -> js number in the file).</summary>
public sealed class AppliedFix
{
    public string ActionMapsPath { get; set; } = "";
    public Dictionary<int, int> Map { get; set; } = new();
    public DateTime AppliedAt { get; set; }

    static string FilePath => Path.Combine(Profile.Folder, "applied.json");
    public static string BackupFolder => Path.Combine(Profile.Folder, "backups");

    public static AppliedFix? Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AppliedFix>(File.ReadAllText(FilePath)) : null; }
        catch { return null; }
    }

    /// <summary>
    /// Renumbers the file so home slots land on <paramref name="target"/> (home -> today's js number), starting from
    /// however <paramref name="current"/> left it. Backs up first. Returns the new state, or null when the file is
    /// back in the home numbering.
    /// </summary>
    public static AppliedFix? Update(string actionMapsPath, AppliedFix? current, Dictionary<int, int> target)
    {
        if (current != null && !string.Equals(current.ActionMapsPath, actionMapsPath, StringComparison.OrdinalIgnoreCase))
        {
            current.Restore();
            current = null;
        }
        var step = current == null ? target : BindingsFixer.Compose(BindingsFixer.Invert(current.Map), target);
        var fix = BindingsFixer.IsIdentity(target) ? null : new AppliedFix { ActionMapsPath = actionMapsPath, Map = target, AppliedAt = DateTime.Now };
        if (BindingsFixer.IsIdentity(step))
        {
            Save(fix);
            return fix;
        }

        Directory.CreateDirectory(BackupFolder);
        var text = File.ReadAllText(actionMapsPath);
        File.WriteAllText(Path.Combine(BackupFolder, $"actionmaps-{DateTime.Now:yyyyMMdd-HHmmss}.xml"), text);
        PruneBackups();

        // Record first: if writing the bindings fails half way, the old state is put back so it still describes the file.
        Save(fix);
        try { WriteAtomic(actionMapsPath, BindingsFixer.Remap(text, step)); }
        catch { Save(current); throw; }
        return fix;
    }

    static void Save(AppliedFix? fix)
    {
        Directory.CreateDirectory(Profile.Folder);
        if (fix == null) File.Delete(FilePath);
        else File.WriteAllText(FilePath, JsonSerializer.Serialize(fix));
    }

    /// <summary>The home slot a js number in the file stands for.</summary>
    public int HomeSlot(int fileSlot) => Map.FirstOrDefault(kv => kv.Value == fileSlot).Key is var k && k != 0 ? k : fileSlot;

    /// <summary>Puts the file back to the home numbering. Keeps any bindings changed in-game meanwhile.</summary>
    public void Restore()
    {
        if (File.Exists(ActionMapsPath))
        {
            var text = File.ReadAllText(ActionMapsPath);
            WriteAtomic(ActionMapsPath, BindingsFixer.Remap(text, BindingsFixer.Invert(Map)));
        }
        File.Delete(FilePath);
    }

    static void WriteAtomic(string path, string text)
    {
        var tmp = path + ".jk-tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }

    static void PruneBackups()
    {
        foreach (var old in new DirectoryInfo(BackupFolder).GetFiles("actionmaps-*.xml")
                     .OrderByDescending(f => f.Name).Skip(30))
            old.Delete();
    }
}
