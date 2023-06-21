using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Tomat.Push.API.Loader;

public sealed class AssemblyResolver : IDisposable {
    private readonly AssemblyLoadContext loadContext;
    private readonly List<AssemblyResolver> dependencies = new();
    private readonly DependencyContext dependencyContext;
    private readonly CompositeCompilationAssemblyResolver resolver;

    public AssemblyResolver(AssemblyLoadContext loadContext, Assembly assembly, string assemblyDirectory) {
        this.loadContext = loadContext;
        dependencyContext = DependencyContext.Load(assembly)
                         ?? throw new InvalidOperationException("Attempted to load dependency context for single-file assembly!");
        resolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[] { new AppBaseCompilationAssemblyResolver(assemblyDirectory), new ReferenceAssemblyPathResolver(), new PackageCompilationAssemblyResolver(), });

        loadContext.Resolving += ResolveAssembly;
    }

    public void AddDependency(AssemblyResolver dependency) {
        dependencies.Add(dependency);
    }

    private Assembly? ResolveAssembly(AssemblyLoadContext alc, AssemblyName assemblyName) {
        // Prioritize dependencies over the current assembly, since it is likely
        // that some of our assembly references will be coming from mods that
        // this mod depends on.
        foreach (var dependency in dependencies) {
            // Note that we use the dependency's AssemblyLoadContext.
            var assembly = dependency.ResolveAssembly(dependency.loadContext, assemblyName);
            if (assembly is not null)
                return assembly;
        }

        var library = dependencyContext.RuntimeLibraries.FirstOrDefault(
            x => string.Equals(x.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)
        );

        // Unfortunately, if we can't resolve it here then it probably just...
        // won't get resolved. We might want to handle this later, TODO?
        if (library is null)
            return null;

        var assemblies = new List<string>();
        var wrapper = new CompilationLibrary(
            library.Type,
            library.Name,
            library.Version,
            library.Hash,
            library.RuntimeAssemblyGroups.SelectMany(x => x.AssetPaths),
            library.Dependencies,
            library.Serviceable
        );
        resolver.TryResolveAssemblyPaths(wrapper, assemblies);
        return assemblies.Count != 0 ? alc.LoadFromAssemblyPath(assemblies[0]) : null;
    }

    public void Dispose() {
        loadContext.Resolving -= ResolveAssembly;
    }
}
