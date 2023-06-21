using System.Runtime.Versioning;

namespace Tomat.Push.API.Platform.Linux;

[SupportedOSPlatform("linux")]
public sealed class LinuxPlatform : UnixPlatform {
    public override string? LocateGamePath() {
        throw new System.NotImplementedException();
    }
}
