using System;
using System.Diagnostics;
using System.IO;

namespace Tomat.Push.Launcher;

internal static class Program {
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

        Process osuProc = Process.Start(new ProcessStartInfo {
            FileName = osuPath,
            Arguments = "--appimage-mount",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        while (!osuProc.StandardOutput.EndOfStream) {
            o = osuProc.StandardOutput.ReadLine();
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
    }
}
