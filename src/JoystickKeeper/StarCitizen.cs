using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JoystickKeeper;

/// <summary>A joystick entry from Star Citizen's actionmaps.xml (&lt;options type="joystick" instance="N" Product="..."&gt;).</summary>
public sealed record ScJoystick(int Instance, string ProductName, int VendorId, int ProductId);

public static class StarCitizen
{
    static readonly string[] Channels = { "LIVE", "PTU", "EPTU", "TECH-PREVIEW", "HOTFIX" };

    /// <summary>Finds actionmaps.xml files for every installed channel, newest first.</summary>
    public static List<string> FindActionMaps()
    {
        var found = new List<string>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var roots = new[]
            {
                Path.Combine(drive.Name, "Program Files", "Roberts Space Industries", "StarCitizen"),
                Path.Combine(drive.Name, "Roberts Space Industries", "StarCitizen"),
                Path.Combine(drive.Name, "Games", "Roberts Space Industries", "StarCitizen"),
                Path.Combine(drive.Name, "StarCitizen"),
            };
            foreach (var root in roots)
                foreach (var ch in Channels)
                {
                    var f = Path.Combine(root, ch, "user", "client", "0", "Profiles", "default", "actionmaps.xml");
                    if (File.Exists(f)) found.Add(f);
                }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(File.GetLastWriteTime).ToList();
    }

    static readonly Regex GuidInProduct = new(@"\{([0-9A-Fa-f]{8})-0000-0000-0000-504944564944\}");

    /// <summary>Reads which physical device the bindings file expects in each js slot.</summary>
    public static List<ScJoystick> ReadJoysticks(string actionMapsPath)
    {
        var doc = XDocument.Load(actionMapsPath);
        var result = new List<ScJoystick>();
        foreach (var opt in doc.Descendants("options"))
        {
            if (!string.Equals((string?)opt.Attribute("type"), "joystick", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse((string?)opt.Attribute("instance"), out var inst)) continue;
            var product = (string?)opt.Attribute("Product") ?? "";
            int vid = 0, pid = 0;
            var m = GuidInProduct.Match(product);
            if (m.Success)
            {
                uint data1 = Convert.ToUInt32(m.Groups[1].Value, 16);
                vid = (int)(data1 & 0xFFFF);
                pid = (int)(data1 >> 16);
            }
            var name = GuidInProduct.Replace(product, "").Trim();
            if (result.All(r => r.Instance != inst))
                result.Add(new ScJoystick(inst, name, vid, pid));
        }
        return result.OrderBy(r => r.Instance).ToList();
    }
}
