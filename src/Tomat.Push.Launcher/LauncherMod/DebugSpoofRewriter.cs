using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using Tomat.Push.API;

namespace Tomat.Push.Launcher.LauncherMod;

public sealed class DebugSpoofRewriter : IModuleRewriter {
    bool IModuleRewriter.RewriteModule(ModuleDefinition module) {
        if (module.Assembly.Name.Name != "osu.Framework")
            return false;

        var debugUtils = module.GetType("osu.Framework.Development.DebugUtils");
        var isDebugAssembly = debugUtils.GetMethods().First(x => x.Name == "isDebugAssembly");
        var il = new ILContext(isDebugAssembly);
        var c = new ILCursor(il);

        c.Emit(OpCodes.Ldc_I4_0);
        c.Emit(OpCodes.Ret);

        return true;
    }
}
