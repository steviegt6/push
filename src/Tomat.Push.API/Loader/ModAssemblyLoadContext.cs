using System.Reflection;
using System.Runtime.Loader;

namespace Tomat.Push.API.Loader;

public sealed class ModAssemblyLoadContext : AssemblyLoadContext {
    internal ModAssemblyLoadContext(string name) : base(name) { }

    protected override Assembly? Load(AssemblyName assemblyName) {
        return base.Load(assemblyName);
    }
}
