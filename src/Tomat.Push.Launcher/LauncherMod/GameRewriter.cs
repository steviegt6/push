using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using Tomat.Push.API;

namespace Tomat.Push.Launcher.LauncherMod;

public sealed class GameWriter : IModuleRewriter {
    private static Version? osuVersion;

    bool IModuleRewriter.RewriteModule(ModuleDefinition module) {
        if (module.Assembly.Name.Name == "osu!") {
            osuVersion = module.Assembly.Name.Version;
            return false;
        }

        if (module.Assembly.Name.Name != "osu.Game")
            return false;

        var osuGameBase = module.GetType("osu.Game.OsuGameBase");
        var getAsmVersion = osuGameBase.GetMethods().First(x => x.Name == "get_AssemblyVersion");
        var il = new ILContext(getAsmVersion);
        var c = new ILCursor(il);
        c.Emit(OpCodes.Call, typeof(GameWriter).GetMethod("GetDesktopVersion", BindingFlags.Public | BindingFlags.Static)!);
        c.Emit(OpCodes.Ret);

        return true;
    }

    public static Version GetDesktopVersion() {
        return osuVersion ?? new Version(0, 0, 0, 0);
    }
}
