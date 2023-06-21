using System;

namespace Tomat.Push.API.Platform;

/// <summary>
///     Represents a platform.
/// </summary>
public interface IPlatform : IDisposable {
    string GetSaveDirectory(string name);
    
    /// <summary>
    ///     Attempts to locate the path of the game.
    /// </summary>
    /// <returns>
    ///     The path of the game, or <see langword="null"/> if the game could
    ///     not be located.
    /// </returns>
    string? LocateGamePath();
}
