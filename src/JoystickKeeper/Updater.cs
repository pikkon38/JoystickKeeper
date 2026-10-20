using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace JoystickKeeper;

public sealed record UpdateInfo(Version Version, string DownloadUrl);

/// <summary>Checks GitHub Releases for a newer JoystickKeeper.exe and swaps it in place of the running copy.</summary>
public static class Updater
{
    const string Repo = "pikkon38/JoystickKeeper";
    const string AssetName = "JoystickKeeper.exe";

    public static Version Current => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0));

    static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JoystickKeeper", Current.ToString()));
        return http;
    }

    /// <summary>Returns the latest release if it is newer than this copy, otherwise null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = NewClient();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
        var asset = root.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase));
        if (asset.ValueKind != JsonValueKind.Object) return null;
        latest = Normalize(latest);
        return latest > Current ? new UpdateInfo(latest, asset.GetProperty("browser_download_url").GetString()!) : null;
    }

    /// <summary>
    /// Downloads the new exe next to the running one, then starts a small script that waits for this process to exit,
    /// replaces the exe and starts it again. The caller should exit right after this returns.
    /// </summary>
    public static async Task DownloadAndStageAsync(UpdateInfo update)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Can't tell where Joystick Keeper is running from.");
        var staged = exe + ".new";
        using (var http = NewClient())
        using (var src = await http.GetStreamAsync(update.DownloadUrl))
        using (var dst = File.Create(staged))
            await src.CopyToAsync(dst);
        if (new FileInfo(staged).Length < 1_000_000)
        {
            File.Delete(staged);
            throw new InvalidOperationException("The downloaded file looks incomplete.");
        }

        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), $"jk-update-{pid}.cmd");
        File.WriteAllText(script, $"""
            @echo off
            :wait
            tasklist /fi "PID eq {pid}" | find "{pid}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            move /y "{staged}" "{exe}" >nul
            start "" "{exe}"
            del "%~f0"
            """.Replace("\n", "\r\n"));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
    }

    static Version Normalize(Version v) => new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
