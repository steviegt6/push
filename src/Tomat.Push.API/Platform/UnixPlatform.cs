using System;
using System.Collections.Generic;
using System.IO;

namespace Tomat.Push.API.Platform;

public abstract class UnixPlatform : Platform {
    protected override IEnumerable<string?> GetUserStoragePaths() {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (!string.IsNullOrEmpty(xdg))
            yield return xdg;

        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share");

        foreach (var path in base.GetUserStoragePaths())
            yield return path;
    }
}
