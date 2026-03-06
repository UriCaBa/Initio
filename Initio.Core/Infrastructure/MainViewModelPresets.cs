using Initio.Core.Models;

namespace Initio.Core.Infrastructure;

internal static class MainViewModelPresets
{
    public static IReadOnlyList<AppDefinition> DefaultCatalog { get; } =
    [
        new("Chrome", "Browsers", "Google.Chrome"),
        new("Firefox", "Browsers", "Mozilla.Firefox"),
        new("Spotify", "Media", "Spotify.Spotify"),
        new("VLC", "Media", "VideoLAN.VLC"),
        new("Discord", "Comms", "Discord.Discord"),
        new("Steam", "Gaming", "Valve.Steam"),
        new("Dropbox", "Cloud", "Dropbox.Dropbox"),
        new("LibreOffice", "Docs", "TheDocumentFoundation.LibreOffice"),
        new("NVIDIA App", "Drivers", "Nvidia.NVIDIAApp"),
        new("Notepad++", "Utilities", "Notepad++.Notepad++"),
        new("Riot Vanguard", "Gaming", "RiotGames.RiotClient"),
        new("Foxit PDF Reader", "Docs", "Foxit.FoxitReader")
    ];

    public static List<SelectionProfile> CreateSelectionProfiles(string defaultProfileKey, string customProfileKey)
    {
        var defaultIds = new HashSet<string>(DefaultCatalog.Select(item => item.WingetId), StringComparer.OrdinalIgnoreCase);
        var devRig = new HashSet<string>(defaultIds, StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.VisualStudioCode", "Git.Git",
            "Docker.DockerDesktop", "OBSProject.OBSStudio",
            "GitHub.GitHubDesktop"
        };
        var homeOffice = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google.Chrome", "Mozilla.Firefox", "Zoom.Zoom",
            "Microsoft.Teams", "SlackTechnologies.Slack",
            "TheDocumentFoundation.LibreOffice", "Foxit.FoxitReader",
            "Dropbox.Dropbox", "Notion.Notion",
            "Discord.Discord", "Notepad++.Notepad++", "Spotify.Spotify"
        };
        var gaming = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Valve.Steam", "EpicGames.EpicGamesLauncher", "GOG.Galaxy",
            "RiotGames.RiotClient", "Discord.Discord",
            "Nvidia.NVIDIAApp", "OBSProject.OBSStudio",
            "Google.Chrome", "Spotify.Spotify"
        };

        return
        [
            new SelectionProfile(defaultProfileKey, "Default", defaultIds),
            new SelectionProfile("dev", "Dev", devRig),
            new SelectionProfile("homeoffice", "Office", homeOffice),
            new SelectionProfile("gaming", "Gaming", gaming),
            new SelectionProfile(customProfileKey, "Custom", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true)
        ];
    }
}
