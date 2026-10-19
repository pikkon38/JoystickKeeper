using System.Text.Json;

namespace JoystickKeeper;

/// <summary>One device in the reference ("golden") setup.</summary>
public sealed class ProfileDevice
{
    public string Key { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    /// <summary>The js number Star Citizen bindings expect this device to have. 0 = not used in game.</summary>
    public int StarCitizenSlot { get; set; }
}

public sealed class Profile
{
    public int Version { get; set; } = 1;
    public DateTime SavedAt { get; set; }
    public List<ProfileDevice> Devices { get; set; } = new();
    public string? RsiLauncherPath { get; set; }
    /// <summary>The actionmaps.xml these slots describe.</summary>
    public string? ActionMapsPath { get; set; }

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JoystickKeeper");

    public static string FilePath => Path.Combine(Folder, "profile.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Profile Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Profile>(File.ReadAllText(FilePath)) ?? new Profile();
        }
        catch { /* unreadable file: start fresh, Save will overwrite it */ }
        return new Profile();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        SavedAt = DateTime.Now;
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    public ProfileDevice? Find(string key) => Devices.FirstOrDefault(d => d.Key == key);
}
