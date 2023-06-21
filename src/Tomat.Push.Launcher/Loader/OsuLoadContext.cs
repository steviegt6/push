using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Mono.Cecil;
using Tomat.Push.API;

namespace Tomat.Push.Launcher.Loader;

public sealed class OsuLoadContext : AssemblyLoadContext {
    private readonly string rootDir;
    private readonly List<IModuleRewriter> rewriters;
    private readonly OsuCecilAssemblyResolver cecilResolver;
    private readonly Dictionary<AssemblyName, Assembly> loadedAssemblies = new();

    public OsuLoadContext(string rootDir, List<IModuleRewriter> rewriters) : base("osu!") {
        this.rootDir = rootDir;
        this.rewriters = rewriters;
        cecilResolver = new OsuCecilAssemblyResolver();
        cecilResolver.AddSearchDirectory(this.rootDir);
    }

    protected override Assembly? Load(AssemblyName assemblyName) {
        if (loadedAssemblies.TryGetValue(assemblyName, out var loadedAsm))
            return loadedAsm;

        // TODO: Finding a way to use AssemblyResolver would be better here.

        Console.WriteLine("Loading osu! assembly: " + assemblyName);
        var dllPath = Path.Combine(rootDir, assemblyName.Name + ".dll");

        if (!File.Exists(dllPath)) {
            Console.WriteLine("Assembly not found at: " + dllPath);
            // return loadedAssemblies[assemblyName] = LoadFromAssemblyName(assemblyName);
            return null;
        }

        Console.WriteLine("Found assembly at: " + dllPath);
        var moduleDefinition = ModuleDefinition.ReadModule(
            dllPath,
            new ReaderParameters {
                AssemblyResolver = cecilResolver,
            }
        );

        var rewritten = false;
        foreach (var rewriter in rewriters)
            rewritten |= rewriter.RewriteModule(moduleDefinition);

        if (rewritten) {
            using var ms = new MemoryStream();
            moduleDefinition.Write(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return loadedAssemblies[assemblyName] = LoadFromStream(ms);
        }

        return loadedAssemblies[assemblyName] = LoadFromAssemblyPath(dllPath);
    }
}
