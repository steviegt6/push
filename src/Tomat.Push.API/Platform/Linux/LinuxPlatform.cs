using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Tomat.Push.API.Utilities;

namespace Tomat.Push.API.Platform.Linux;

[SupportedOSPlatform("linux")]
public sealed class LinuxPlatform : UnixPlatform {
    private readonly List<Process?> processes = new();

    public override string? LocateGamePath() {
        var whichOutput = ProcUtil.RunCommandAndGetOutput("which", "osu-lazer");
        if (whichOutput is null)
            throw new Exception("Unable to run `which`.");

        string? osuPath = null;

        foreach (var line in whichOutput) {
            if (string.IsNullOrEmpty(line))
                continue;

            // TODO: Localization needed?
            if (line.Contains("not found"))
                break;

            if (File.Exists(line)) {
                osuPath = line;
                break;
            }
        }

        if (string.IsNullOrEmpty(osuPath) || !File.Exists(osuPath)) {
            string? input = null;

            while (input is null || !File.Exists(input)) {
                Console.Write("The `osu-lazer` AppImage is not on the path, please enter the path to your `osu-lazer` AppImage:");
                input = Console.ReadLine();
            }
        }

        var osuMountProc = ProcUtil.RunCommand(osuPath!, "--appimage-mount");
        if (osuMountProc is null)
            throw new Exception($"Unable to mount AppImage: '{osuPath}'.");

        processes.Add(osuMountProc);

        string? osuMountOutput = null!;
        while (!osuMountProc.StandardOutput.EndOfStream) {
            string? o = osuMountProc.StandardOutput.ReadLine();

            if (!string.IsNullOrEmpty(o)) {
                osuMountOutput = o;
                break;
            }
        }

        if (osuMountOutput is null)
            throw new Exception($"Unable to mount AppImage: '{osuPath}'.");

        return Path.Combine(osuMountOutput, "usr/bin/osu!.dll");
    }

    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);

        if (!disposing)
            return;

        foreach (var proc in processes) {
            if (proc is not null && !proc.HasExited)
                proc.Kill();
        }
    }
}
