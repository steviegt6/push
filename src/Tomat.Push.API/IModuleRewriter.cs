using Mono.Cecil;

namespace Tomat.Push.API;

public interface IModuleRewriter {
    public bool RewriteModule(ModuleDefinition module);
}
