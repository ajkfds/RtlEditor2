using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Regression tests for <c>pluginVerilog.Data.SimulationSetup</c>.
    ///
    /// The NavigatePanel (VerilogCommon.Updater) and SimulationSetup walk the
    /// same instance hierarchy, but through different data:
    ///   - Updater uses BuildingBlock.GetBuildingBlockInstantiations() and
    ///     builds one instance per building block when a file declares several.
    ///   - SimulationSetup used to look the child instance up in file.Items,
    ///     which only holds the internal instantiations when the file declares
    ///     exactly ONE building block.
    /// A file with several building blocks therefore lost its whole subtree in
    /// the setup even though the NavigatePanel could expand it.
    /// </summary>
    public class SimulationSetupTests : IDisposable
    {
        private readonly string _root;

        public SimulationSetupTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "svlsp_simsetup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { /* best effort */ }
        }

        private static void EnsurePluginRegistered()
        {
            if (CodeEditor2.Global.FileTypes.Count == 0)
            {
                new pluginVerilog.Plugin().Register();
            }
        }

        private class TestProject : CodeEditor2.Data.Project
        {
            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public TestProject(string name, string path) : base(name, path, "") { }
        }

        /// <summary>
        /// Builds a VerilogFile backed by a real file on disk and parses it with
        /// the real (UI-free) parser.
        /// </summary>
        private static async Task<pluginVerilog.Data.VerilogFile> CreateParsedFile(
            CodeEditor2.Data.Project project, string relativePath, string text)
        {
            string absolute = project.GetAbsolutePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, text);

            pluginVerilog.Data.VerilogFile file = new pluginVerilog.Data.VerilogFile()
            {
                Name = Path.GetFileName(relativePath),
                Project = project,
                RelativePath = relativePath
            };
            file.SystemVerilog = true;

            pluginVerilog.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);
            return file;
        }

        private static CodeEditor2.Data.Project CreateProject(string name, string root)
        {
            TestProject project = new TestProject(name, root);
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            return project;
        }

        /// <summary>
        /// A file that declares several building blocks (here: a package plus
        /// the top module) must still contribute the modules instantiated by
        /// the top module to the setup. file.Items holds one instance per
        /// building block in that case, so the old file.Items lookup missed
        /// the child module entirely.
        /// </summary>
        [Fact]
        public async Task MultiBuildingBlockFile_CollectsInstantiatedModule()
        {
            EnsurePluginRegistered();

            CodeEditor2.Data.Project project = CreateProject("simsetup_multi", _root);

            // leaf module in its own file
            await CreateParsedFile(project, "leaf.sv",
                """
                module leaf;
                endmodule
                """);

            // top file declares a package AND the top module that instances leaf
            pluginVerilog.Data.VerilogFile top = await CreateParsedFile(project, "top.sv",
                """
                package pkg;
                endpackage

                module top;
                    leaf u_leaf();
                endmodule
                """);

            pluginVerilog.Data.SimulationSetup? setup = pluginVerilog.Data.SimulationSetup.Create(top);
            Assert.NotNull(setup);

            // the instantiated leaf module's source file must be collected
            Assert.Contains(setup!.Files, f => f.RelativePath.EndsWith("leaf.sv", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// A stale "unfound module" entry (recorded by an earlier parse pass and
        /// never cleared) must not drop the whole subtree of the file. The
        /// NavigatePanel expands the resolved instances of that same file, so
        /// the setup has to collect them too.
        /// </summary>
        [Fact]
        public async Task StaleUnfoundEntry_DoesNotDropTheSubtree()
        {
            EnsurePluginRegistered();

            CodeEditor2.Data.Project project = CreateProject("simsetup_unfound", _root);

            await CreateParsedFile(project, "leaf.sv",
                """
                module leaf;
                endmodule
                """);

            pluginVerilog.Data.VerilogFile top = await CreateParsedFile(project, "top.sv",
                """
                module top;
                    leaf u_leaf();
                endmodule
                """);

            // simulate a stale unfound entry left over from an earlier pass
            top.VerilogParsedDocument!.UnfoundModules.Add("some_removed_module");

            pluginVerilog.Data.SimulationSetup? setup = pluginVerilog.Data.SimulationSetup.Create(top);
            Assert.NotNull(setup);

            // the resolved leaf instance must still be collected
            Assert.Contains(setup!.Files, f => f.RelativePath.EndsWith("leaf.sv", StringComparison.OrdinalIgnoreCase));
            // and the stale entry is surfaced instead of silently ignored
            Assert.Contains(setup.UnfoundModules, m => m.Contains("some_removed_module", StringComparison.Ordinal));
        }
    }
}
