using System.IO;
using System.Reflection;

namespace FamVault.App.Services;

internal static class AssemblyResolver
{
    private static readonly string[] AllowedAssemblies =
    {
        "System.Text.Json",
        "System.Text.Encodings.Web",
        "System.Threading.Tasks.Extensions",
        "System.Memory",
        "System.Buffers",
        "System.Runtime.CompilerServices.Unsafe",
        "Microsoft.Bcl.AsyncInterfaces",
        "System.Numerics.Vectors",
        "System.ValueTuple"
    };

    public static void Register()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        AssemblyName requested = new(args.Name);

        if (Array.IndexOf(AllowedAssemblies, requested.Name) < 0)
            return null;

        string folder = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location);
        string filePath = Path.Combine(folder, requested.Name + ".dll");

        if (!File.Exists(filePath))
            return null;

        AssemblyName available = AssemblyName.GetAssemblyName(filePath);

        if (!string.Equals(available.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
            return null;

        return Assembly.LoadFrom(filePath);
    }
}
