using System;
using System.Collections.Generic;
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
        throw new System.NotImplementedException();
    }
}
