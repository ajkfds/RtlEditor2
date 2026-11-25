using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Lists every non-comment line mentioning ProjectProperty in the parser, so each
// can be replaced by the project-settings contract member it uses.
internal static class Program
{
    private static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : ".";
        if (!Directory.Exists(root)) { Console.Error.WriteLine("usage: _tmp_pp2 <dir>"); return 2; }

        int files = 0, lines = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                                          .OrderBy(p => p, StringComparer.Ordinal))
        {
            string[] all = File.ReadAllLines(path);
            var hits = new List<(int No, string Text)>();
            for (int i = 0; i < all.Length; i++)
            {
                string t = all[i].TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) ||
                    t.StartsWith("///", StringComparison.Ordinal) ||
                    t.StartsWith("*", StringComparison.Ordinal)) continue;
                if (t.StartsWith("using ", StringComparison.Ordinal)) continue;
                if (all[i].Contains("ProjectProperty", StringComparison.Ordinal)) hits.Add((i + 1, all[i].Trim()));
            }
            if (hits.Count == 0) continue;
            files++; lines += hits.Count;
            Console.WriteLine($"=== {Path.GetFileName(path)} ({hits.Count}) ===");
            foreach (var h in hits) Console.WriteLine($"  {h.No,5} {h.Text}");
        }
        Console.WriteLine();
        Console.WriteLine($"files {files} / lines {lines}");
        return 0;
    }
}
