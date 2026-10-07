
using System;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Phase 10: parser-backed adapter tests.
    /// Drives the real CodeEditor2VerilogPlugin parser (VerilogParser /
    /// ParsedDocument) through the UI-free CoreBridge.ParseEngine and verifies
    /// that the CoreBridge adapters expose the parse results through the
    /// UI-agnostic SystemVerilogCore interfaces.
    /// </summary>
    public class ParserBackedAdapterTests
    {
        /// <summary>
        /// Registers the plugin's file types with CodeEditor2.Global.
        /// Register() is UI-free (menu setup is in Initialize()) so it is
        /// safe to call from a test process.
        /// </summary>
        private static void EnsurePluginRegistered()
        {
            if (CodeEditor2.Global.FileTypes.Count == 0)
            {
                new pluginVerilog.Plugin().Register();
            }
        }

        /// Minimal UI-free project stub. The Project constructor is protected;
        /// a derived class in the test assembly can call it without running
        /// Project.CreateAsync (which depends on the Avalonia dispatcher).
        /// </summary>
        private class TestProject : CodeEditor2.Data.Project
        {
            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public TestProject() : base("test", "C:\\does-not-exist", "") { }
        }

        /// <summary>
        /// Builds a VerilogFile without touching the editor UI (no
        /// Project.CreateAsync / FileCheckAsync; those paths depend on the
        /// Avalonia dispatcher).
        /// </summary>
        private static pluginVerilog.Data.VerilogFile CreateFile(CodeEditor2.Data.Project project, string relativePath)
        {
            pluginVerilog.Data.VerilogFile file = new pluginVerilog.Data.VerilogFile()
            {
                Name = "top.sv",
                Project = project,
                RelativePath = relativePath
            };
            file.SystemVerilog = true;
            return file;
        }

        [Fact]
        public async Task ParseEngine_ParseAndFindDefinition()
        {
            EnsurePluginRegistered();

            CodeEditor2.Data.Project project = new TestProject();

            // Normally registered by Plugin.projectCreated (fired from
            // Project.CreateAsync); the UI-free test path bypasses it.
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "top.sv");

            string text =
"""
module top;
    wire a;
    wire b;
    assign a = 1'b1;
endmodule
""";

            // parse with the real (UI-free) engine
            pluginVerilog.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);
            Assert.NotNull(parsed!.Root);

           // drive the CoreBridge adapters
           var fileAdapter = new pluginVerilog.CoreBridge.SystemVerilogFileAdapter(file);
           var document = new pluginVerilog.CoreBridge.SystemVerilogDocumentAdapter(fileAdapter, parsed);

           // documentSymbol: top module must appear as a top-level block.
           Assert.Contains(document.Root.BuildingBlocks.Values, b => b.Name == "top");
       }

       [Fact]
       public async Task InMemoryCore_ReferencesFromRealParser()
       {
           EnsurePluginRegistered();

           var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
           var project = (SystemVerilogLanguageServer.Server.InMemoryProject)await core.GetProjectAsync("default");

           string text =
"""
module top;
    wire sig;
    wire dst;
    assign dst = sig;
    wire sink;
    assign sink = sig;
endmodule
""";

           project.AddOrUpdateFile("mem://top.sv", null, text, true);
           var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)project.FindFile("mem://top.sv")!;

           // find the declaration of "sig" (first occurrence in the text)
           int sigIndex = text.IndexOf("sig", StringComparison.Ordinal);
           var refs = await project.FindReferencesAsync(memFile, sigIndex);

           // declaration + 2 use sites ("assign dst = sig", "assign sink = sig")
           Assert.True(refs.Count >= 3, $"expected at least 3 references, got {refs.Count}");

           // all locations should refer to the identifier "sig"
           foreach (var r in refs)
           {
               Assert.Equal("sig", r.Name);
           }
       }

       [Fact]
       public async Task InMemoryCore_CompletionFromRealParser()
       {
           EnsurePluginRegistered();

           var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
           var project = (SystemVerilogLanguageServer.Server.InMemoryProject)await core.GetProjectAsync("default");

           string text =
"""
module top;
    wire mysignal;
    wire dst;
    assign dst = my;
endmodule
""";

           project.AddOrUpdateFile("mem://completion.sv", null, text, true);
           var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)project.FindFile("mem://completion.sv")!;

           // caret right after "my" (incomplete candidate word)
           int index = text.IndexOf("my", StringComparison.Ordinal) + 1;
           var entries = project.GetCompletionItems(memFile, index);

           Assert.True(entries != null,
               $"entries is null; docLen={memFile.CodeDocument.Length}, index={index}, textLen={text.Length}");
           // the partial parse must produce concrete candidates (keywords at
   // minimum; DataObject items appear for expression / LHS positions)
   Assert.NotEmpty(entries!);
   Assert.Contains(entries!, e => e.Text == "begin");
       }

      [Fact]
      public void InMemoryCore_DiagnosticsFromRealParser()
      {
          EnsurePluginRegistered();

          var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
          var project = (SystemVerilogLanguageServer.Server.InMemoryProject)core.GetOrCreateProject("default");

          // undeclared identifier "dst" produces a parse diagnostic
          string text =
"""
module top;
    wire sig;
    assign dst = 1;
endmodule
""";

          project.AddOrUpdateFile("mem://diag.sv", null, text, true);
          var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)project.FindFile("mem://diag.sv")!;
          memFile.ForceBuild();

          var doc = project.GetDocument(memFile);
          Assert.NotNull(doc);
          Assert.True(doc!.Diagnostics.Count > 0, "expected at least one diagnostic (undeclared dst)");
      }
  }
}
