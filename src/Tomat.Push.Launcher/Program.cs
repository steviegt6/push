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

namespace Tomat.Push.Launcher;

internal class OsuResolver : IAssemblyResolver {
    private DefaultAssemblyResolver resolver = new();

    public AssemblyDefinition Resolve(AssemblyNameReference name) {
        Console.WriteLine("[push] Loading assembly using OsuResolver " + name);
        string dllPath = Path.Combine(Program.osuRoot, name.Name + ".dll");

        if (!File.Exists(dllPath))
            return resolver.Resolve(name);

        Console.WriteLine("[push] Assembly " + name.Name + " found at " + dllPath);
        ModuleDefinition md = ModuleDefinition.ReadModule(dllPath, new ReaderParameters { AssemblyResolver = this });
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
        ModuleDefinition md = ModuleDefinition.ReadModule(dllPath, new ReaderParameters { AssemblyResolver = mdResolver });

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
    public static string osuDllPath = null!;
    public static string osuRoot = null!;
    private static string _modsPath = null!;
    private static string modsPath {
        get {
            if (!string.IsNullOrEmpty(_modsPath))
                return _modsPath;

            if (OperatingSystem.IsWindows())
                _modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "push", "mods");
            else if (OperatingSystem.IsMacOS()) {
                string aps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");
                if (Directory.Exists(aps))
                    _modsPath = Path.Combine(aps, "push", "mods");
                else {
                    string? xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

                    if (!string.IsNullOrEmpty(xdgData))
                        _modsPath = Path.Combine(xdgData, "push", "mods");
                    else
                        _modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share", "push", "mods");
                }
            }
            else if (OperatingSystem.IsLinux()) {
                string? xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

                if (!string.IsNullOrEmpty(xdgData))
                    _modsPath = Path.Combine(xdgData, "push", "mods");
                else
                    _modsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share", "push", "mods");
            }
            else
                throw new System.NotImplementedException();

            return _modsPath;
        }
    }

    private static List<Assembly> mods = null!;
    private static Dictionary<Type, object> rewriters = null!;

    public static Version OsuVersion() => osuDesktopAssembly.GetName().Version ?? new();

    private static string LinuxPath() {
        string osuPath = "";
        Process proc = Process.Start(new ProcessStartInfo {
            FileName = "which",
            Arguments = "osu-lazer",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        string? o = null;
        while (!proc.StandardOutput.EndOfStream) {
            o = proc.StandardOutput.ReadLine();

            if (Directory.Exists(o))
                break;
        }
        proc.WaitForExit();

        if (!string.IsNullOrEmpty(o) && !o.Contains("not found"))
            osuPath = o!;

        if (string.IsNullOrEmpty(osuPath)) {
            Console.Write("The osu-lazer appimage is not on the path, please enter the path to your osu-lazer appimage:");
            string? i;
            while ((i = Console.ReadLine()) == null && File.Exists(i))
                Console.Write("Invalid input. Try again: ");

            osuPath = i!;
        }

        mountProc = Process.Start(new ProcessStartInfo {
            FileName = osuPath,
            Arguments = "--appimage-mount",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        while (!mountProc.StandardOutput.EndOfStream) {
            o = mountProc.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(o))
                return Path.Combine(o, "usr/bin/osu!.dll");
        }

        throw new System.Exception("Unable to mount appimage " + osuPath);
    }

    private static string WindowsPath() {
        return "";
    }

    private static string MacPath() {
        throw new System.NotImplementedException("Mac is not supported!");
    }

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

        if (!Directory.Exists(modsPath))
            Directory.CreateDirectory(modsPath);

        if (OperatingSystem.IsWindows())
            osuDllPath = WindowsPath();
        else if (OperatingSystem.IsMacOS())
            osuDllPath = MacPath();
        else if (OperatingSystem.IsLinux())
            osuDllPath = LinuxPath();
        else
            throw new System.NotImplementedException();
        osuRoot = Directory.GetParent(osuDllPath)!.ToString();

        Console.WriteLine("Launching osu!.dll located at " + osuDllPath);

        OsuAlc alc = new();

        mods = GetModAssemblies(modsPath, alc);
        rewriters = CreateRewriterInstances();

        osuDesktopAssembly = alc.LoadFromAssemblyPath(osuDllPath);
        osuDesktopAssembly.GetType("osu.Desktop.Program")!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { args });
    }
}
