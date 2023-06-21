using Mono.Cecil;

namespace Tomat.Push.API;

public interface IModuleRewriter {
    bool RewriteModule(ModuleDefinition module);
}
