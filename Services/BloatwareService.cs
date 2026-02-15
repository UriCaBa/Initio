using System.Diagnostics;
using NewPCSetupWPF.Models;

namespace NewPCSetupWPF.Services;

public static class BloatwareService
{
    private const int DetectionTimeoutSeconds = 30;
    private const int RemovalTimeoutSeconds = 60;

    private static readonly IReadOnlyList<BloatwareDefinition> KnownBloatware =
    [
        // ═══ Games ═══
        new("Candy Crush Saga", "Games", "king.com.CandyCrushSaga", "Pre-installed mobile game"),
        new("Candy Crush Friends", "Games", "king.com.CandyCrushFriends", "Pre-installed mobile game"),
        new("Bubble Witch 3 Saga", "Games", "king.com.BubbleWitch3Saga", "Pre-installed mobile game"),
        new("Farm Heroes Saga", "Games", "king.com.FarmHeroesSaga", "Pre-installed mobile game"),
        new("March of Empires", "Games", "A278AB0D.MarchofEmpires", "Pre-installed strategy game"),
        new("Microsoft Solitaire", "Games", "Microsoft.MicrosoftSolitaireCollection", "Card game with ads"),
        new("Minecraft (Trial)", "Games", "Microsoft.MinecraftEducationEdition", "Trial/education edition"),

        // ═══ Social & Entertainment ═══
        new("Disney+", "Social & Entertainment", "Disney.37853FC22B2CE", "Streaming app promotion"),
        new("Spotify (Pre-installed)", "Social & Entertainment", "SpotifyAB.SpotifyMusic", "Pre-installed promotion"),
        new("TikTok", "Social & Entertainment", "BytedancePte.Ltd.TikTok", "Pre-installed social media"),
        new("Instagram", "Social & Entertainment", "Facebook.Instagram", "Pre-installed social media"),
        new("Facebook", "Social & Entertainment", "Facebook.Facebook", "Pre-installed social media"),
        new("Messenger", "Social & Entertainment", "Facebook.Messenger", "Pre-installed messenger"),
        new("Netflix", "Social & Entertainment", "4DF9E0F8.Netflix", "Streaming promotion"),
        new("Amazon Prime Video", "Social & Entertainment", "AmazonVideo.PrimeVideo", "Streaming promotion"),
        new("Twitter", "Social & Entertainment", "9E2F88E3.Twitter", "Pre-installed social media"),
        new("LinkedIn", "Social & Entertainment", "Microsoft.LinkedIn", "Pre-installed professional network"),
        new("WhatsApp", "Social & Entertainment", "5319275A.WhatsAppDesktop", "Pre-installed messenger"),

        // ═══ Microsoft Bloat ═══
        new("News", "Microsoft Bloat", "Microsoft.BingNews", "Bing News aggregator"),
        new("Weather", "Microsoft Bloat", "Microsoft.BingWeather", "Bing Weather widget"),
        new("Finance", "Microsoft Bloat", "Microsoft.BingFinance", "Bing Finance widget"),
        new("Sports", "Microsoft Bloat", "Microsoft.BingSports", "Bing Sports widget"),
        new("Maps", "Microsoft Bloat", "Microsoft.WindowsMaps", "Windows Maps (rarely used)"),
        new("People", "Microsoft Bloat", "Microsoft.People", "Contacts app"),
        new("Groove Music", "Microsoft Bloat", "Microsoft.ZuneMusic", "Legacy music player"),
        new("Movies & TV", "Microsoft Bloat", "Microsoft.ZuneVideo", "Legacy video player"),
        new("Mail and Calendar", "Microsoft Bloat", "microsoft.windowscommunicationsapps", "Legacy mail app"),
        new("Mixed Reality Portal", "Microsoft Bloat", "Microsoft.MixedReality.Portal", "VR headset portal"),
        new("3D Viewer", "Microsoft Bloat", "Microsoft.Microsoft3DViewer", "3D model viewer"),
        new("Paint 3D", "Microsoft Bloat", "Microsoft.MSPaint", "Legacy 3D paint app"),
        new("OneNote (Win10)", "Microsoft Bloat", "Microsoft.Office.OneNote", "Legacy OneNote"),
        new("Skype", "Microsoft Bloat", "Microsoft.SkypeApp", "Legacy Skype"),
        new("Clipchamp", "Microsoft Bloat", "Clipchamp.Clipchamp", "Video editor promotion"),
        new("Power Automate", "Microsoft Bloat", "Microsoft.PowerAutomateDesktop", "RPA tool"),
        new("Microsoft Family", "Microsoft Bloat", "MicrosoftCorporationII.MicrosoftFamily", "Parental control app"),

        // ═══ Promotions ═══
        new("Xbox Game Bar", "Promotions", "Microsoft.XboxGamingOverlay", "Gaming overlay"),
        new("Xbox Identity Provider", "Promotions", "Microsoft.XboxIdentityProvider", "Xbox login service"),
        new("Xbox Console Companion", "Promotions", "Microsoft.XboxApp", "Legacy Xbox companion"),
        new("Feedback Hub", "Promotions", "Microsoft.WindowsFeedbackHub", "Microsoft feedback tool"),
        new("Get Help", "Promotions", "Microsoft.GetHelp", "Microsoft help app"),
        new("Tips", "Promotions", "Microsoft.Getstarted", "Windows tips and tricks"),
        new("Phone Link", "Promotions", "Microsoft.YourPhone", "Phone-to-PC linking app"),
    ];

    public static IReadOnlyList<BloatwareItem> GetKnownBloatware()
    {
        return KnownBloatware.Select(b => new BloatwareItem
        {
            Name = b.Name,
            Category = b.Category,
            PackageName = b.PackageName,
            Description = b.Description
        }).ToList();
    }

    public static async Task<HashSet<string>> DetectInstalledAsync(
        IReadOnlyList<BloatwareItem> items,
        CancellationToken ct = default)
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = await RunPowerShellAsync(
            "Get-AppxPackage | Select-Object -ExpandProperty Name",
            DetectionTimeoutSeconds, ct);

        if (string.IsNullOrWhiteSpace(output)) return installed;

        var installedNames = new HashSet<string>(
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var found = installedNames.Any(name =>
                name.Contains(item.PackageName, StringComparison.OrdinalIgnoreCase));
            item.IsInstalled = found;
            item.RemovalStatus = found ? "Detected" : "Not Found";
            if (found) installed.Add(item.PackageName);
        }

        return installed;
    }

    public static async Task<bool> RemovePackageAsync(
        string packageName,
        CancellationToken ct = default)
    {
        if (!InputValidation.IsValidPackageName(packageName)) return false;

        var script = $"Get-AppxPackage '*{packageName}*' | Remove-AppxPackage -ErrorAction Stop";
        var output = await RunPowerShellAsync(script, RemovalTimeoutSeconds, ct);
        return output is not null;
    }

    public static async Task<bool> VerifyRemovedAsync(
        string packageName,
        CancellationToken ct = default)
    {
        if (!InputValidation.IsValidPackageName(packageName)) return false;

        var output = await RunPowerShellAsync(
            $"Get-AppxPackage '*{packageName}*' | Select-Object -ExpandProperty Name",
            10, ct);
        return string.IsNullOrWhiteSpace(output);
    }

    private static async Task<string?> RunPowerShellAsync(
        string script, int timeoutSeconds, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var output = await outputTask;
            var error = await errorTask;

            return process.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    internal sealed record BloatwareDefinition(
        string Name, string Category, string PackageName, string Description);
}
