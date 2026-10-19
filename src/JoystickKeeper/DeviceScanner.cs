using Vortice.DirectInput;

namespace JoystickKeeper;

/// <summary>A game controller as DirectInput (and therefore Star Citizen) reports it right now.</summary>
public sealed class ScannedDevice
{
    /// <summary>1-based position in DirectInput enumeration order.</summary>
    public int Slot { get; init; }
    public string ProductName { get; init; } = "";
    public string InstanceName { get; init; } = "";
    public int VendorId { get; init; }
    public int ProductId { get; init; }
    public Guid ProductGuid { get; init; }
    public Guid InstanceGuid { get; init; }
    public string InterfacePath { get; init; } = "";

    /// <summary>Xbox-style (XInput) controllers. Star Citizen treats these as a gamepad, not a joystick, so they get no js slot.</summary>
    public bool IsXInput =>
        InterfacePath.Contains("IG_", StringComparison.OrdinalIgnoreCase) ||
        (InterfacePath.Length == 0 && ProductName.Contains("Xbox", StringComparison.OrdinalIgnoreCase));

    /// <summary>The js number Star Citizen will give this device right now (0 = not a joystick to the game).</summary>
    public int GameSlot { get; set; }

    /// <summary>Stable identity across reboots and replugs: VID:PID, plus #n when several identical devices are attached.</summary>
    public string Key { get; set; } = "";

    public string VidPid => $"{VendorId:X4}:{ProductId:X4}";

    public string Kind => VendorId switch
    {
        0x231D => "VKB",
        0x2341 or 0x2A03 or 0x1B4F => "Arduino",
        0x1234 when ProductId == 0xBEAD => "vJoy (virtual)",
        0x1532 => "Razer",
        _ => "",
    };
}

public static class DeviceScanner
{
    /// <summary>
    /// Lists attached game controllers in DirectInput order. Star Citizen numbers the non-XInput ones js1, js2... in this order.
    /// </summary>
    public static List<ScannedDevice> Scan(IDirectInput8 di)
    {
        var list = new List<ScannedDevice>();
        var seen = new Dictionary<string, int>();
        int slot = 0, gameSlot = 0;
        foreach (var inst in di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
        {
            slot++;
            var (vid, pid) = SplitProductGuid(inst.ProductGuid);
            var dev = new ScannedDevice
            {
                Slot = slot,
                ProductName = inst.ProductName?.Trim() ?? "",
                InstanceName = inst.InstanceName?.Trim() ?? "",
                VendorId = vid,
                ProductId = pid,
                ProductGuid = inst.ProductGuid,
                InstanceGuid = inst.InstanceGuid,
                InterfacePath = ReadInterfacePath(di, inst.InstanceGuid),
            };
            if (!dev.IsXInput) dev.GameSlot = ++gameSlot;
            var baseKey = dev.VidPid;
            seen[baseKey] = seen.TryGetValue(baseKey, out var n) ? n + 1 : 1;
            dev.Key = seen[baseKey] == 1 ? baseKey : $"{baseKey}#{seen[baseKey]}";
            list.Add(dev);
        }
        return list;
    }

    static string ReadInterfacePath(IDirectInput8 di, Guid instance)
    {
        try
        {
            using var d = di.CreateDevice(instance);
            return d.Properties.InterfacePath ?? "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// DirectInput product GUIDs for HID devices look like {PPPPVVVV-0000-0000-0000-504944564944}
    /// ("PIDVID" in the last block). Star Citizen writes this same GUID into actionmaps.xml.
    /// </summary>
    public static (int vid, int pid) SplitProductGuid(Guid g)
    {
        var bytes = g.ToByteArray();
        uint data1 = BitConverter.ToUInt32(bytes, 0);
        return ((int)(data1 & 0xFFFF), (int)(data1 >> 16));
    }
}
