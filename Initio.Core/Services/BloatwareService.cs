using Initio.Core.Abstractions;
using Initio.Core.Models;

namespace Initio.Core.Services;

public sealed class BloatwareService : IBloatwareService
{
    private const int DetectionTimeoutSeconds = 30;
    private const int RemovalTimeoutSeconds = 60;

    private static readonly IReadOnlyList<BloatwareDefinition> KnownBloatware =
    [
        new("Candy Crush Saga", "Games", "king.com.CandyCrushSaga", "Pre-installed mobile game"),
        new("Candy Crush Friends", "Games", "king.com.CandyCrushFriends", "Pre-installed mobile game"),
        new("Bubble Witch 3 Saga", "Games", "king.com.BubbleWitch3Saga", "Pre-installed mobile game"),
        new("Farm Heroes Saga", "Games", "king.com.FarmHeroesSaga", "Pre-installed mobile game"),
        new("March of Empires", "Games", "A278AB0D.MarchofEmpires", "Pre-installed strategy game"),
        new("Microsoft Solitaire", "Games", "Microsoft.MicrosoftSolitaireCollection", "Card game with ads"),
        new("Minecraft (Trial)", "Games", "Microsoft.MinecraftEducationEdition", "Trial/education edition"),
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
        new("News", "Microsoft Bloat", "Microsoft.BingNews", "Bing News aggregator"),
        new("Weather", "Microsoft Bloat", "Microsoft.BingWeather", "Bing Weather widget"),
        new("Finance", "Microsoft Bloat", "Microsoft.BingFinance", "Bing Finance widget"),
        new("Sports", "Microsoft Bloat", "Microsoft.BingSports", "Bing Sports widget"),
        new("Maps", "Microsoft Bloat", "Microsoft.WindowsMaps", "Windows Maps"),
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
        new("Xbox Game Bar", "Promotions", "Microsoft.XboxGamingOverlay", "Gaming overlay"),
        new("Xbox Identity Provider", "Promotions", "Microsoft.XboxIdentityProvider", "Xbox login service"),
        new("Xbox Console Companion", "Promotions", "Microsoft.XboxApp", "Legacy Xbox companion"),
        new("Feedback Hub", "Promotions", "Microsoft.WindowsFeedbackHub", "Microsoft feedback tool"),
        new("Get Help", "Promotions", "Microsoft.GetHelp", "Microsoft help app"),
        new("Tips", "Promotions", "Microsoft.Getstarted", "Windows tips and tricks"),
        new("Phone Link", "Promotions", "Microsoft.YourPhone", "Phone-to-PC linking app")
    ];

    private readonly IProcessRunner _processRunner;
    private readonly string? _powerShellExecutablePath;

    public BloatwareService(IProcessRunner processRunner, string? powerShellExecutablePath = null)
    {
        _processRunner = processRunner;
        _powerShellExecutablePath = string.IsNullOrWhiteSpace(powerShellExecutablePath)
            ? WindowsCommandPaths.TryGetPowerShellPath()
            : powerShellExecutablePath;
    }

    public IReadOnlyList<BloatwareDefinition> GetKnownBloatware() => KnownBloatware;

    public async Task<HashSet<string>> DetectInstalledAsync(IReadOnlyCollection<string> packageNames, CancellationToken cancellationToken = default)
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (packageNames.Count == 0 || !HasTrustedExecutablePath(_powerShellExecutablePath))
        {
            return installed;
        }

        var result = await _processRunner.RunAsync(
            new ProcessSpec(
                _powerShellExecutablePath!,
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage | Select-Object -ExpandProperty Name\""),
            TimeSpan.FromSeconds(DetectionTimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        var output = result?.CombinedOutput;
        if (string.IsNullOrWhiteSpace(output))
        {
            return installed;
        }

        var installedNames = new HashSet<string>(
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        foreach (var packageName in packageNames)
        {
            if (installedNames.Contains(packageName) ||
                installedNames.Any(name => name.Contains(packageName, StringComparison.OrdinalIgnoreCase)))
            {
                installed.Add(packageName);
            }
        }

        return installed;
    }

    public async Task<bool> RemovePackageAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (!InputValidation.IsValidPackageName(packageName) || !HasTrustedExecutablePath(_powerShellExecutablePath))
        {
            return false;
        }

        var result = await _processRunner.RunAsync(
            new ProcessSpec(
                _powerShellExecutablePath!,
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"$packages = Get-AppxPackage -Name '{packageName}' -ErrorAction SilentlyContinue; if ($null -eq $packages) {{ exit 1 }}; $packages | ForEach-Object {{ Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop }}\""),
            TimeSpan.FromSeconds(RemovalTimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        return result is not null && result.ExitCode == 0;
    }

    public async Task<bool> VerifyRemovedAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (!InputValidation.IsValidPackageName(packageName) || !HasTrustedExecutablePath(_powerShellExecutablePath))
        {
            return false;
        }

        var result = await _processRunner.RunAsync(
            new ProcessSpec(
                _powerShellExecutablePath!,
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name '{packageName}' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty PackageFullName\""),
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);

        return result is not null && result.ExitCode == 0 && string.IsNullOrWhiteSpace(result.CombinedOutput);
    }

    private static bool HasTrustedExecutablePath(string? executablePath)
    {
        return !string.IsNullOrWhiteSpace(executablePath) &&
            Path.IsPathRooted(executablePath) &&
            File.Exists(executablePath);
    }
}

