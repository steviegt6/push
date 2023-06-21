using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Tomat.Push.API;
using Tomat.Push.API.Platform;
using Tomat.Push.API.Platform.Linux;
using Tomat.Push.API.Platform.Mac;
using Tomat.Push.API.Platform.Windows;

namespace Tomat.Push.Launcher;

internal class OsuResolver : IAssemblyResolver {
    private DefaultAssemblyResolver resolver = new();

    public AssemblyDefinition Resolve(AssemblyNameReference name) {
        Console.WriteLine("[push] Loading assembly using OsuResolver " + name);
        string dllPath = Path.Combine(Program.osuRoot, name.Name + ".dll");

        if (!File.Exists(dllPath))
            return resolver.Resolve(name);

        Console.WriteLine("[push] Assembly " + name.Name + " found at " + dllPath);
        ModuleDefinition md = ModuleDefinition.ReadModule(dllPath,
                                                          new ReaderParameters {
                                                              AssemblyResolver = this
                                                          });
        if (Program.RewriteModule(md))
            return md.Assembly;
        else
            return AssemblyDefinition.ReadAssembly(dllPath);
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters _) {
        return Resolve(name);
    }

    public void Dispose() {
        resolver.Dispose();
    }
}

internal class OsuAlc : AssemblyLoadContext {
    private class DefaultAlc : AssemblyLoadContext { }

    private OsuResolver mdResolver = new();
    private DefaultAlc alc = new();

    protected override Assembly? Load(AssemblyName assemblyName) {
        Console.WriteLine("[push] Loading assembly using OsuAlc " + assemblyName);
        string dllPath = Path.Combine(Program.osuRoot, assemblyName.Name + ".dll");

        if (!File.Exists(dllPath))
            return alc.LoadFromAssemblyName(assemblyName);

        Console.WriteLine("[push] Assembly " + assemblyName.Name + " found at " + dllPath);
        ModuleDefinition md = ModuleDefinition.ReadModule(dllPath,
                                                          new ReaderParameters {
                                                              AssemblyResolver = mdResolver
                                                          });

        if (assemblyName.Name == "osu.Game") {
            Console.WriteLine("[push] osu.Game patched!");
            MethodDefinition meth = md.GetType("osu.Game.OsuGameBase").Methods.First(m => m.Name == "get_AssemblyVersion");
            ILContext il = new(meth);
            ILCursor c = new(il);
            c.Emit(OpCodes.Call, typeof(Program).GetMethod("OsuVersion", BindingFlags.Public | BindingFlags.Static)!);
            c.Emit(OpCodes.Ret);
        }

        if (Program.RewriteModule(md) || assemblyName.Name == "osu.Game") {
            using MemoryStream stream = new();
            md.Write(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return this.LoadFromStream(stream);
        }

        return this.LoadFromAssemblyPath(dllPath);
    }
}

public static class Program {
    private static Process mountProc = null!;
    private static Assembly osuDesktopAssembly = null!;
    public static string osuRoot = null!;

    private static List<Assembly> mods = null!;
    private static Dictionary<Type, object> rewriters = null!;

    public static Version OsuVersion() => osuDesktopAssembly.GetName().Version ?? new();

    private static List<Assembly> GetModAssemblies(string modsPath, AssemblyLoadContext ctx) {
        List<Assembly> ret = new();

        foreach (string dir in Directory.GetDirectories(modsPath)) {
            string name = Path.GetFileName(dir);

            string modAsmPath = Directory.GetFiles(dir).First(f => f.EndsWith(name + ".dll"));
            Console.WriteLine("[push] Detected mod " + name);
            ret.Add(ctx.LoadFromAssemblyPath(modAsmPath));
        }

        return ret;
    }

    private static Dictionary<Type, object> CreateRewriterInstances() {
        Dictionary<Type, object> ret = new();

        foreach (Assembly asm in mods) {
            var rewriters = asm.GetTypes().Where(t => t.GetInterfaces().Contains(typeof(IModuleRewriter)));

            foreach (Type rewriter in rewriters) {
                ret.Add(rewriter, Activator.CreateInstance(rewriter)!);
            }
        }

        return ret;
    }

    internal static bool RewriteModule(ModuleDefinition md) {
        if (rewriters == null) {
            Console.WriteLine("[push] Module " + md.Name + " loaded too early to be rewritten");
            return false;
        }

        bool rewroteAny = false;

        foreach (var (type, instance) in rewriters) {
            bool rewrote = (bool)type.GetMethod("RewriteModule")!
                                     .Invoke(instance, new object[] { md })!;

            if (rewrote)
                rewroteAny = true;
        }

        return rewroteAny;
    }

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

        // TODO
        /*OsuAlc alc = new();

        mods = GetModAssemblies(modsPath, alc);
        rewriters = CreateRewriterInstances();

        osuDesktopAssembly = alc.LoadFromAssemblyPath(osuDllPath);
        osuDesktopAssembly.GetType("osu.Desktop.Program")!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { args });*/
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
