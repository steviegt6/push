using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Tomat.Push.API.Loader;
using Tomat.Push.API.Platform;
using Tomat.Push.API.Platform.Linux;
using Tomat.Push.API.Platform.Mac;
using Tomat.Push.API.Platform.Windows;
using Tomat.Push.Launcher.Loader;

// GameWriter exposes an internal field to osu.Game.
[assembly: InternalsVisibleTo("osu.Game")]

namespace Tomat.Push.Launcher;

public static class Program {
    internal static void Main(string[] args) {
        Console.WriteLine("Welcome to push!");

        var platform = GetPlatform();
        if (platform is null)
            throw new PlatformNotSupportedException("Unknown or unsupported platform!");

        Console.WriteLine($"Using platform '{platform.GetType().Name}' ({platform.GetType().FullName}).");
        var storagePath = platform.GetSaveDirectory("push");
        var modsPath = Path.Combine(storagePath, "mods");
        Console.WriteLine($"Using storage path: '{storagePath}'.");
        Console.WriteLine($"Using mods path: '{modsPath}'.");

        Directory.CreateDirectory(storagePath);
        Directory.CreateDirectory(modsPath);

        Console.WriteLine("Loading mods...");
        var launcherResolver = new AssemblyResolver(
            AssemblyLoadContext.Default,
            typeof(Program).Assembly,
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!
        );

        var launcherMod = new Mod(typeof(Program).Assembly, AssemblyLoadContext.Default, launcherResolver);

        var mods = ModOrganizer.LoadModsFromDirectory(modsPath, launcherResolver, launcherMod);

        var anyMods = mods.Count > 0;
        var singleMod = mods.Count == 1;
        Console.WriteLine($"Loaded {mods.Count} {(singleMod ? "mod" : "mods")}{(anyMods ? ':' : '.')}");

        foreach (var mod in mods) {
            Console.WriteLine($"    {mod.Name} v{mod.Version}");

            foreach (var dependency in mod.Dependencies)
                Console.WriteLine($"        Depends on {dependency.Name} v{dependency.Version}");
        }

        foreach (var mod in mods) {
            Console.WriteLine($"Running initializers for {mod.Name}...");

            foreach (var initializer in mod.Initializers) {
                Console.WriteLine($"    Running initializer '{initializer.GetType().FullName}'...");
                initializer.Initialize();
            }
        }

        Console.WriteLine("Finished running mod initializers!");

        Console.WriteLine("Attempting to locate game path...");
        var gamePath = platform.LocateGamePath();

        while (gamePath is null || !File.Exists(gamePath)) {
            Console.WriteLine("Failed to locate game path. Please enter the path to your osu!.dll:");
            gamePath = Console.ReadLine();

            // In case someone passes a directory when prompted instead...
            if (Directory.Exists(gamePath))
                gamePath = Path.Combine(gamePath, "osu!.dll");
        }

        var gameRoot = Path.GetDirectoryName(gamePath)!;

        Console.WriteLine("Located game path: " + gamePath);
        Console.WriteLine("Located game root: " + gameRoot);

        var osuLoadContext = new OsuLoadContext(gameRoot, mods.SelectMany(x => x.Rewriters).ToList());
        var osuAsm = osuLoadContext.LoadFromAssemblyName(new AssemblyName("osu!"));
        if (osuAsm is null)
            throw new FileNotFoundException("Failed to load osu! assembly!");

        osuAsm.GetType("osu.Desktop.Program")!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { args });
    }

    private static IPlatform? GetPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsPlatform();

        if (OperatingSystem.IsMacOS())
            return new MacPlatform();

        if (OperatingSystem.IsLinux())
            return new LinuxPlatform();

        return null;
    }
}
