using System;
using System.Collections.Generic;

namespace Tomat.Push.API.Loader; 

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ModDependencyAttribute : Attribute {
    public string Name { get; }
    
    public Version Version { get; }

    public ModDependencyAttribute(string name, int major, int minor, int patch) {
        Name = name;
        Version = new Version(major, minor, patch);
    }

    public ModIdentity MakeIdentity() {
        return new ModIdentity(Name, Version, new List<ModIdentity>());
    }
}
