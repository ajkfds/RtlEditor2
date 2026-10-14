
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
        /// Returns true when the test depends on UVM (uvm_pkg / uvm_macros.svh),
        /// which is not yet supported. These tests are skipped until the
        /// UVM package and macros are provided.
        /// </summary>
        internal static bool IsUvmTest(string path, string text)
        {
            if (text.Contains("uvm_pkg", StringComparison.Ordinal)) return true;
            if (text.Contains("uvm_macros.svh", StringComparison.Ordinal)) return true;
            string relative = Path.GetFullPath(path);
            string uvmDir = Path.Combine(FindSvTestsRoot(), "uvm");
            if (relative.StartsWith(Path.GetFullPath(uvmDir), StringComparison.OrdinalIgnoreCase)) return true;
            if (Path.GetFileName(path).IndexOf("uvm", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
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

        // ------------------------------------------------------------------
        // Shared project: every .sv / .svh under sv_tests is registered as a
        // project item (with its directory structure preserved) so that
        // include directives can resolve relative paths through
        // Project.GetItem / FindItems (WordScanner.parseInclude).
        // ------------------------------------------------------------------
        private static readonly object _projectLock = new object();
        private static CodeEditor2.Data.Project? _sharedProject;
        private static readonly Dictionary<string, pluginVerilog.Data.VerilogFile> _svFileTable =
            new Dictionary<string, pluginVerilog.Data.VerilogFile>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, pluginVerilog.Data.VerilogHeaderFile> _svhFileTable =
            new Dictionary<string, pluginVerilog.Data.VerilogHeaderFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the shared project (building it on first use) and the
        /// registered VerilogFile for the given absolute test file path.
        /// </summary>
        internal static (CodeEditor2.Data.Project Project, pluginVerilog.Data.VerilogFile File)
            GetProjectAndFile(string absolutePath)
        {
            EnsurePluginRegistered();

            string root = FindSvTestsRoot();
            string relative = Path.GetRelativePath(root, absolutePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            lock (_projectLock)
            {
                if (_sharedProject == null) BuildSharedProject(root);

                pluginVerilog.Data.VerilogFile? file;
                if (!_svFileTable.TryGetValue(relative, out file) || file == null)
                {
                    throw new InvalidOperationException($"test file not registered in shared project: {relative}");
                }
                return (_sharedProject!, file);
            }
        }

        private static void BuildSharedProject(string root)
        {
            CodeEditor2.Data.Project project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            _sharedProject = project;

            // enumerate .sv and .svh (include targets) files
            List<string> paths = new List<string>();
            paths.AddRange(Directory.EnumerateFiles(root, "*.sv", SearchOption.AllDirectories));
            paths.AddRange(Directory.EnumerateFiles(root, "*.svh", SearchOption.AllDirectories));
            paths.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                string relative = Path.GetRelativePath(root, path)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');

                string name = Path.GetFileName(relative);
                string dir = "";
                int slash = relative.LastIndexOf('/');
                if (slash >= 0) dir = relative.Substring(0, slash);

                CodeEditor2.Data.Folder parentFolder = EnsureFolder(project, dir);
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch
                {
                    continue; // unreadable file: skip registration
                }

                if (relative.EndsWith(".svh", StringComparison.OrdinalIgnoreCase))
                {
                    pluginVerilog.Data.VerilogHeaderFile header = new pluginVerilog.Data.VerilogHeaderFile()
                    {
                        Name = name,
                        Project = project,
                        RelativePath = relative
                    };
                    SetCodeDocumentText(header, text);
                    parentFolder.Items.AddOrUpdate(name, header);
                    _svhFileTable[relative] = header;
                }
                else
                {
                    pluginVerilog.Data.VerilogFile file = new pluginVerilog.Data.VerilogFile()
                    {
                        Name = name,
                        Project = project,
                        RelativePath = relative,
                        SystemVerilog = true
                    };
                    SetCodeDocumentText(file, text);
                    parentFolder.Items.AddOrUpdate(name, file);
                    _svFileTable[relative] = file;
                }
            }
        }

        /// <summary>
        /// Creates intermediate folders for the given directory-relative path
        /// ("a/b/c") under the project and returns the deepest folder.
        /// </summary>
        private static CodeEditor2.Data.Folder EnsureFolder(CodeEditor2.Data.Project project, string dirRelative)
        {
            if (string.IsNullOrEmpty(dirRelative)) return project;

            string[] parts = dirRelative.Split('/');
            CodeEditor2.Data.Item current = project;
            string accumulated = "";
            foreach (string part in parts)
            {
                accumulated = (accumulated.Length == 0) ? part : accumulated + "/" + part;

                CodeEditor2.Data.Item? found;
                if (current.Items.TryGetValue(part, out found) && found != null)
                {
                    current = found;
                    continue;
                }
                CodeEditor2.Data.Folder folder = CodeEditor2.Data.Folder.Create(accumulated, project, current);
                current.Items.AddOrUpdate(part, folder);
                current = folder;
            }
            return (CodeEditor2.Data.Folder)current;
        }

        /// <summary>
        /// Creates a CodeDocument with the given text and assigns it to the
        /// TextFile. The CodeDocument property setter is not public in
        /// UI-less hosts (FileCheckAsync is UI dependent), so reflection is
        /// used (same approach as ParseEngine.CreateCodeDocument).
        /// </summary>
        private static void SetCodeDocumentText(CodeEditor2.Data.TextFile textFile, string text)
        {
            // pluginVerilog.CodeDocument only exposes (IVerilogRelatedFile) and
            // (string) constructors; the text must be set (diveIntoIncludeFile
            // snapshots this document for the include parse)
            pluginVerilog.CodeEditor.CodeDocument document =
                new pluginVerilog.CodeEditor.CodeDocument(text);
            var property = typeof(CodeEditor2.Data.TextFile).GetProperty(
                "CodeDocument",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
            property?.SetValue(textFile, document);
        }
    }
}
