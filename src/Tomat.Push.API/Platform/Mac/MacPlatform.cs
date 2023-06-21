using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace Tomat.Push.API.Platform.Mac;

[SupportedOSPlatform("macos")]
public sealed class MacPlatform : UnixPlatform {
    protected override IEnumerable<string?> GetUserStoragePaths() {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");

        foreach (var path in base.GetUserStoragePaths())
            yield return path;
    }

    public override string? LocateGamePath() {
        throw new System.NotImplementedException();
    }
}
