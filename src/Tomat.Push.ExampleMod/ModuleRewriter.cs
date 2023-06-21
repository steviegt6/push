using Mono.Cecil;
using MonoMod.Cil;
using System;
using System.Linq;
using Tomat.Push.API;

namespace Tomat.Push.ExampleMod;

public class ModuleRewriter : IModuleRewriter {
    public bool RewriteModule(ModuleDefinition module) {
        if (module.Name != "osu.Game")
            return false;

        Console.WriteLine("[push ExampleMod] Rewriting module " + module);

        MethodDefinition meth = module.GetType("osu.Game.Screens.Play.Player")!.Methods.First(m => m.Name == "LoadComplete");
        ILContext il = new(meth);
        ILCursor c = new(il);
        c.EmitDelegate(() => Console.WriteLine("Hello from ExampleMod!! Player Loaded??"));

        return true;
    }
}
