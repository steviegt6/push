using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using Tomat.Push.API;

namespace Tomat.Push.Launcher.LauncherMod;

public sealed class SentryDisablerRewriter : IModuleRewriter {
    bool IModuleRewriter.RewriteModule(ModuleDefinition module) {
        if (module.Assembly.Name.Name != "Sentry")
            return false;

        var sentryOptions = module.GetType("Sentry.SentryOptions");

        var getDsn = sentryOptions.GetMethods().First(x => x.Name == "get_Dsn");
        var getDsnIl = new ILContext(getDsn);
        var getDsnC = new ILCursor(getDsnIl);
        getDsnC.Emit(OpCodes.Ldnull);
        getDsnC.Emit(OpCodes.Ret);

        var setDsn = sentryOptions.GetMethods().First(x => x.Name == "set_Dsn");
        var setDsnIl = new ILContext(setDsn);
        var setDsnC = new ILCursor(setDsnIl);
        setDsnC.Emit(OpCodes.Ret);

        var sentrySdk = module.GetType("Sentry.SentrySdk");

        var isEnabled = sentrySdk.GetMethods().First(x => x.Name == "get_IsEnabled");
        var isEnabledIl = new ILContext(isEnabled);
        var isEnabledC = new ILCursor(isEnabledIl);
        isEnabledC.Emit(OpCodes.Ldc_I4_0);
        isEnabledC.Emit(OpCodes.Ret);

        return true;
    }
}
