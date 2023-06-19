using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Tomat.Push.Launcher;

internal class OsuAlc : AssemblyLoadContext {  }

public static class Program {
    private static Process mountProc = null!;
    private static Assembly osuDesktopAssembly = null!;
    private static string osuDllPath = null!;
    private static string osuRoot = null!;

    public static Version OsuVersion() => osuDesktopAssembly.GetName().Version ?? new();
    public static Version Guh = new Version(2022, 20, 2, 2);

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

    internal static void Main(string[] args) {
        Console.WriteLine("Welcome to push!");

        try {
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
            alc.Resolving += Resolving;

            osuDesktopAssembly = alc.LoadFromAssemblyPath(osuDllPath);
            osuDesktopAssembly.GetType("osu.Desktop.Program")!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { new string[0] });
        }
        finally {
            Console.WriteLine("Killing mount process if needed");
            mountProc?.Kill();
        }
    }

    private static Assembly? Resolving(AssemblyLoadContext context, AssemblyName name) {
        string assemblyPath = Path.Combine(osuRoot, name.Name! + ".dll");
        Console.WriteLine("[push] Loading " + assemblyPath);

        if (name.Name == "osu.Game") {
            DefaultAssemblyResolver res = new DefaultAssemblyResolver();
            res.AddSearchDirectory(osuRoot);
            ModuleDefinition osuGameModule = ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters {
                AssemblyResolver = res,
            });
            MethodDefinition meth = osuGameModule.GetType("osu.Game.OsuGameBase").Methods.First(m => m.Name == "get_AssemblyVersion");
            ILContext il = new(meth);
            ILCursor c = new(il);
            c.Emit(OpCodes.Call, typeof(Program).GetMethod("OsuVersion", BindingFlags.Public | BindingFlags.Static)!);
            c.Emit(OpCodes.Ret);

            using MemoryStream stream = new();
            osuGameModule.Write(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return context.LoadFromStream(stream);
        }

        return context.LoadFromAssemblyPath(assemblyPath);;
    }
}
