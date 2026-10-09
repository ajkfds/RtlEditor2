
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Harness that runs the real VerilogParser over every .sv file in
    /// CodeEditor2VerilogPlugin/verilog_sample/sv_tests and reports the
    /// parse diagnostics.
    /// </summary>
    public class SvTestsParserHarness
    {
        /// <summary>
        /// Registers the plugin's file types with CodeEditor2.Global.
        /// </summary>
        internal static void EnsurePluginRegistered()
        {
            if (CodeEditor2.Global.FileTypes.Count == 0)
            {
                new pluginVerilog.Plugin().Register();
            }
        }

        /// Minimal UI-free project stub.
        internal class TestProject : CodeEditor2.Data.Project
        {
            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public TestProject() : base("test", "C:\\does-not-exist", "") { }
        }

        internal static pluginVerilog.Data.VerilogFile CreateFile(CodeEditor2.Data.Project project, string relativePath)
        {
            return new pluginVerilog.Data.VerilogFile()
            {
                Name = Path.GetFileName(relativePath),
                Project = project,
                RelativePath = relativePath,
                SystemVerilog = true
            };
        }

        /// <summary>
        /// Walks up from the test binary directory until the sv_tests root
        /// (containing a README.md) is found.
        /// </summary>
        internal static string FindSvTestsRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "CodeEditor2VerilogPlugin", "verilog_sample", "sv_tests");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "README.md")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("sv_tests root not found; set the path manually.");
        }

        /// <summary>
        /// Enumerates every .sv test file under the sv_tests root.
        /// </summary>
        internal static List<string> EnumerateSvFiles()
        {
            string root = FindSvTestsRoot();
            return Directory.EnumerateFiles(root, "*.sv", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
