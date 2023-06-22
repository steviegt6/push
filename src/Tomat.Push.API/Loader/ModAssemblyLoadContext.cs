using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Tomat.Push.API.Loader;

public sealed class ModAssemblyLoadContext : AssemblyLoadContext {
    private readonly AssemblyResolver loaderResolver;

    internal ModAssemblyLoadContext(string name, AssemblyResolver loaderResolver) : base(name) {
        this.loaderResolver = loaderResolver;
    }

    protected override Assembly? Load(AssemblyName assemblyName) {
        var loaderAttempt = loaderResolver.ResolveAssembly(assemblyName);
        if (loaderAttempt is not null)
            return loaderAttempt;

        try {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch {
            // ignore
        }

        return base.Load(assemblyName);
    }
}
