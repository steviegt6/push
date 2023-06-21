using System.Collections.Generic;
using System.Diagnostics;

namespace Tomat.Push.API.Utilities;

internal static class ProcUtil {
    public static string[]? RunCommandAndGetOutput(string commandName, string arguments) {
        var proc = Process.Start(new ProcessStartInfo {
            FileName = commandName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });

        if (proc is null)
            return null;

        var output = new List<string>();

        while (!proc.StandardOutput.EndOfStream) {
            var line = proc.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(line))
                output.Add(line);
        }

        proc.WaitForExit();
        return output.ToArray();
    }

    public static Process? RunCommand(string commandName, string arguments) {
        return Process.Start(new ProcessStartInfo {
            FileName = commandName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        });
    }

    public static string[]? GetNonTerminatingOutput(this Process proc) {
        var output = new List<string>();

        while (!proc.StandardOutput.EndOfStream) {
            var line = proc.StandardOutput.ReadLine();
            if (!string.IsNullOrEmpty(line))
                output.Add(line);
        }
        
        return output.ToArray();
    }
}
