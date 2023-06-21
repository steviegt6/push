using Mono.Cecil;

namespace Tomat.Push.Launcher.Loader;

public sealed class OsuCecilAssemblyResolver : BaseAssemblyResolver {
    private readonly DefaultAssemblyResolver defaultResolver = new();

    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) {
        var def = base.Resolve(name, parameters);
        if (def is null)
            return defaultResolver.Resolve(name, parameters);

        return def;
    }
}
