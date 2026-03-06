using System.IO;
using System.Reflection;
using Initio.Core.Services;
using Initio.Core.ViewModels;

namespace NewPCSetupWPF.Services;

public static class AppServiceFactory
{
    public static MainViewModel CreateProductionViewModel()
    {
        var processRunner = new ProcessRunner();
        var embeddedCatalogJson = LoadEmbeddedCatalogJson();
        var wingetPath = WindowsCommandPaths.TryGetWingetPath();
        var powerShellPath = WindowsCommandPaths.TryGetPowerShellPath();
        var catalogService = new CatalogService(embeddedCatalogJson);
        var wingetClient = new WingetClient(processRunner, wingetPath);
        var bloatwareService = new BloatwareService(processRunner, powerShellPath);
        return new MainViewModel(catalogService, wingetClient, bloatwareService);
    }

    public static MainViewModel CreateTestViewModel()
    {
        return new MainViewModel(new FakeCatalogService(), new FakeWingetClient(), new FakeBloatwareService());
    }

    private static string LoadEmbeddedCatalogJson()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("catalog.json");
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
