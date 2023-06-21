using System;
using System.Collections.Generic;
using System.IO;

namespace Tomat.Push.API.Platform;

public abstract class Platform : IPlatform {
    public virtual string GetSaveDirectory(string name) {
        foreach (var path in GetUserStoragePaths()) {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                return Path.Combine(path, name);
        }

        throw new DirectoryNotFoundException("Could not find a valid save directory.");
    }

    protected virtual IEnumerable<string?> GetUserStoragePaths() {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
    }

    public abstract string? LocateGamePath();

    protected virtual void Dispose(bool disposing) {
        if (disposing) { }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
