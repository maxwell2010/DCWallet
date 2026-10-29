using System.Text.Json;

namespace DCWallet;
internal sealed class WalletPreferences
{
    public string Theme { get; set; } = "light";
    public string AccountName { get; set; } = "Основной";
    public int AutoLockMinutes { get; set; } = 5;
    public bool MinimizeToTray { get; set; }
    public bool CloseToTray { get; set; }
    public bool? CheckForUpdatesWindows { get; set; }
    public string GasPriceCeilingGwei { get; set; } = "";
    public Dictionary<string, string> Contacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public static WalletPreferences Load(string directory)
    {
        try { return JsonSerializer.Deserialize<WalletPreferences>(File.ReadAllText(Path.Combine(directory, "preferences.json"))) ?? new(); }
        catch { return new(); }
    }
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "preferences.json"), JsonSerializer.Serialize(this));
    }
}
