
using System.Reflection;

string nuget = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.nuget\packages";

string openaiPkg = Path.Combine(nuget, "openai");
string meaiPkg = Path.Combine(nuget, "microsoft.extensions.ai.openai");
Console.WriteLine("openai versions: " + (Directory.Exists(openaiPkg) ? string.Join(", ", Directory.GetDirectories(openaiPkg).Select(Path.GetFileName)) : "none"));
Console.WriteLine("meai.openai versions: " + (Directory.Exists(meaiPkg) ? string.Join(", ", Directory.GetDirectories(meaiPkg).Select(Path.GetFileName)) : "none"));

string? openaiDll = FindDll(openaiPkg, "OpenAI.dll");
string? meaiDll = FindDll(meaiPkg, "Microsoft.Extensions.AI.OpenAI.dll");
Console.WriteLine($"OpenAI.dll: {openaiDll ?? "NOT FOUND"}");
Console.WriteLine($"MEAI.OpenAI: {meaiDll ?? "NOT FOUND"}");

if (openaiDll != null)
{
    Assembly openai = Assembly.LoadFrom(openaiDll);
    var optsType = openai.GetType("OpenAI.OpenAIClientOptions")!;
    Console.WriteLine("\n--- OpenAIClientOptions ---");
    foreach (var p in optsType.GetProperties())
        Console.WriteLine($"  prop: {p.PropertyType.Name} {p.Name}");
    foreach (var m in optsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine($"  method: {m.Name}");
}
else
{
    Console.WriteLine("OpenAI.dll not found; cannot reflect.");
}

static string? FindDll(string pkgRoot, string fileName)
{
    if (!Directory.Exists(pkgRoot)) return null;
    foreach (var verDir in Directory.GetDirectories(pkgRoot))
    {
        foreach (var tfmDir in Directory.GetDirectories(Path.Combine(verDir, "lib")))
        {
            var candidate = Path.Combine(tfmDir, fileName);
            if (File.Exists(candidate)) return candidate;
        }
    }
    return null;
}
