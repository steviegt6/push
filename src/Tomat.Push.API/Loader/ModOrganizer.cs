using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Medallion.Collections;

namespace Tomat.Push.API.Loader;

public sealed record ModIdentity(string Name, Version Version, List<ModIdentity> Dependencies) {
    public bool Equals(ModIdentity? other) {
        return other is not null && other.Name == Name;
    }

    public override int GetHashCode() {
        // TODO: Incorporate Version? Maybe Dependencies... eh.
        return Name.GetHashCode();
    }
}

public sealed class Mod {
    public Assembly Assembly { get; }

    // TODO: If .GetName().X is ever null, we have bigger issues.
    public string Name => Assembly.GetName().Name!;

    public Version Version => Assembly.GetName().Version!;

    public AssemblyLoadContext LoadContext { get; }

    public AssemblyResolver Resolver { get; }

    public List<IModInitializer> Initializers { get; } = new();

    public List<IModuleRewriter> Rewriters { get; } = new();

    public List<ModIdentity> Dependencies { get; } = new();

    public Mod(Assembly assembly, AssemblyLoadContext loadContext, AssemblyResolver resolver) {
        Assembly = assembly;
        LoadContext = loadContext;
        Resolver = resolver;
    }

    public ModIdentity MakeIdentity() {
        return new ModIdentity(Name, Version, Dependencies.Select(x => new ModIdentity(x.Name, x.Version, new List<ModIdentity>())).ToList());
    }

    public override int GetHashCode() {
        // TODO: Quick and dirty, we probably want to at least differentiate
        // between versions later.
        return Name.GetHashCode();
    }
}

public static class ModOrganizer {
    public static List<Mod> LoadModsFromDirectory(string modDirectory, AssemblyResolver loaderResolver, Mod loaderMod) {
        var directories = Directory.GetDirectories(modDirectory);
        var mods = new Dictionary<string, Mod> {
            { loaderMod.Name, loaderMod },
        };

        foreach (var directory in directories) {
            var modName = Path.GetFileName(directory);
            var modDll = Path.Combine(directory, $"{modName}.dll");

            // TODO: warn
            if (!File.Exists(modDll))
                continue;

            var modLoadContext = new ModAssemblyLoadContext(modName);
            var assembly = modLoadContext.LoadFromAssemblyPath(modDll);
            var resolver = new AssemblyResolver(modLoadContext, assembly, directory);
            resolver.AddDependency(loaderResolver);

            var mod = new Mod(assembly, modLoadContext, resolver);
            assembly.GetCustomAttributes<ModDependencyAttribute>().ToList().ForEach(x => mod.Dependencies.Add(x.MakeIdentity()));

            mods[modName] = mod;
        }

        var modIdentities = mods.Values.Select(x => x.MakeIdentity());
        var sortedIdentities = modIdentities.StableOrderTopologicallyBy(x => x.Dependencies);
        var sortedMods = sortedIdentities.Select(x => mods[x.Name]).ToList();

        foreach (var mod in sortedMods) {
            foreach (var dependency in mod.Dependencies) {
                if (!mods.ContainsKey(dependency.Name))
                    throw new InvalidOperationException($"Mod {mod.Name} depends on mod {dependency.Name}, but it is not loaded!");

                // TODO: log versions 'n' stuff
                var dependencyMod = mods[dependency.Name];
                if (dependencyMod.Assembly.GetName().Version < dependency.Version)
                    throw new InvalidOperationException($"Mod {mod.Name} depends on mod {dependency.Name}, but the version is too low!");

                mod.Resolver.AddDependency(dependencyMod.Resolver);
            }

            foreach (var type in mod.Assembly.GetTypes()) {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                if (!typeof(IModInitializer).IsAssignableFrom(type) && !typeof(IModuleRewriter).IsAssignableFrom(type))
                    continue;

                var instance = Activator.CreateInstance(type)!;

                if (instance is IModInitializer initializer)
                    mod.Initializers.Add(initializer);

                if (instance is IModuleRewriter rewriter)
                    mod.Rewriters.Add(rewriter);
            }
        }

        return sortedMods;
    }
}
