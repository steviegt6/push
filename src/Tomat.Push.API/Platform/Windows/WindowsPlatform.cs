using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace Tomat.Push.API.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatform : Platform {
    protected override IEnumerable<string?> GetUserStoragePaths() {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // The above path is guaranteed to exist, so something's wrong if we
        // ever need a fallback.
        /*foreach (var path in base.GetUserStoragePaths())
            yield return path;*/
    }

    public override string? LocateGamePath() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var lazerDir = Path.Combine(localAppData, "osulazer");

        if (!Directory.Exists(lazerDir))
            return PromptUserInput();

        var appDirs = Directory.GetDirectories(lazerDir, "app-*");
        if (appDirs.Length == 0)
            return PromptUserInput();

        // Assume sorted by version, so the last one is the latest
        // (alphabetically).
        var latest = appDirs[^1];
        if (File.Exists(Path.Combine(latest, "osu!.dll")))
            return Path.Combine(latest, "osu!.dll");

        return PromptUserInput();
    }

    private static string PromptUserInput() {
        string? input = null;

        while (input is null || !File.Exists(input)) {
            Console.WriteLine("Unable to locate osu!lazer installation. Please enter the path to your osu!lazer installation:");
            input = Console.ReadLine();
        }

        return input;
    }
}
