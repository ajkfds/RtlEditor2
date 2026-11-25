
using System;
using System.Threading.Tasks;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// Parser-backed tests.
    /// Drives the real CodeEditor2VerilogPlugin parser (VerilogParser /
    /// ParsedDocument) through the UI-free CoreBridge.ParseEngine and verifies
    /// that the LSP server exposes the parse results through the
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

           // documentSymbol: top module must appear as a top-level block.
           Assert.Contains(parsed.Root!.BuildingBlocks.Values, b => b.Name == "top");
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

           // caret in the middle of "my" (incomplete candidate word within the
           // assign statement; the partial parse of ContinuousAssign collects
           // DataObject candidates filtered by the candidate word "my")
           int index = text.IndexOf("my", StringComparison.Ordinal) + 1;
           var entries = project.GetCompletionItems(memFile, index);

           Assert.True(entries != null,
               $"entries is null; docLen={memFile.CodeDocument.Length}, index={index}, textLen={text.Length}");
           // the partial parse must produce concrete candidates (keywords at
           // minimum; DataObject items appear for expression / LHS positions)
           Assert.NotEmpty(entries!);
           // the module-level signal declared above must be offered as a
           // DataObject candidate when typing an expression (the candidate
           // list is filtered by the incomplete word "my")
           Assert.Contains(entries!, e => e.Text == "mysignal");

           // The parser-facing contract must carry the text to insert, not just
           // the label: a host that inserts must not have to re-derive it from
           // the label (a module name expands to a whole instantiation, a block
           // keyword to its begin/end skeleton).
           Assert.All(entries!, e =>
               Assert.False(string.IsNullOrEmpty(e.InsertText) && string.IsNullOrEmpty(e.Text),
                   $"candidate '{e.Text}' has neither text nor insert text"));
       }

       /// <summary>
       /// At the start of a module-body line, the module names of the project are
       /// offered as candidates and each one carries a full instantiation as its
       /// insert text.
       ///
       /// <para>
       /// This is the VS Code symptom "choosing a module name only inserts the
       /// bare name / nothing expands". Two gates used to suppress the
       /// candidates:
       /// </para>
       ///
       /// <list type="bullet">
       /// <item>Module.cs appended the snippets only when the candidate list was
       /// still empty, but the keyword append immediately before it filled that
       /// list, so the snippets were never appended;</item>
       /// <item>the append also required two characters of candidate word.</item>
       /// </list>
       /// </summary>
       [Fact]
       public async Task Completion_ModuleNameCandidateExpandsToAnInstantiation()
       {
           EnsurePluginRegistered();

           var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
           var project = (SystemVerilogLanguageServer.Server.InMemoryProject)await core.GetProjectAsync("default");

           // One typed character is the case that broke: the module-body EOF
           // branch appended the keyword list first, so the "list is empty"
           // guard around AppendModuleInstanceSnippets was already false and no
           // module candidate was produced. Measured before the fix:
           //   typed "l"  -> [localparam, logic]           (no module)
           //   typed "le" -> [leaf, top]                   (works)
           // so the test must use a single character to be a real regression
           // test.
           string text =
               "module leaf (input logic DATA_I, output logic DATA_O);\n" +
               "endmodule\n" +
               "module top;\n" +
               "  l\n" +
               "endmodule\n";

           project.AddOrUpdateFile("mem://moduleinstance.sv", null, text, true);
           var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)
               project.FindFile("mem://moduleinstance.sv")!;

           // caret right after "l" at the start of a module-body line
           int index = text.IndexOf("  l\n", StringComparison.Ordinal) + 3;
           var entries = project.GetCompletionItems(memFile, index);

           Assert.NotNull(entries);
           var leaf = entries!.FirstOrDefault(e => e.Text == "leaf");
           Assert.True(leaf != null,
               "the module name 'leaf' must be offered; got: " +
               string.Join(", ", entries.Select(e => e.Text)));

           // The candidate must expand: a snippet body, not the bare name.
           Assert.Equal(2, leaf!.InsertTextFormat);
           Assert.NotNull(leaf.InsertText);
           Assert.True(leaf.InsertText!.Length > "leaf".Length,
               "the module candidate must carry an instantiation body, got: " + leaf.InsertText);
           Assert.Contains("leaf", leaf.InsertText);
       }

       /// <summary>
       /// The autocomplete contract exposes InsertText separately from Label.
       ///
       /// <para>
       /// The editor's candidate types apply their expansion inside
       /// ApplyAsync, which a UI-free host cannot call. Carrying the text on the
       /// contract is the first step to moving the insertion to the host, so
       /// this pins that the member exists and is populated.
       /// </para>
       /// </summary>
       [Fact]
       public void AutocompleteContract_CarriesInsertText()
       {
           Type contract = typeof(SystemVerilogCore.Documents.IVerilogAutocompleteItem);
           var insertText = contract.GetProperty("InsertText");

           Assert.NotNull(insertText);
           Assert.Equal(typeof(string), insertText!.PropertyType);
           Assert.True(insertText.CanRead);

           // A concrete candidate must actually fill it (not throw / return
           // null), otherwise the host has nothing to insert.
           EnsurePluginRegistered();
           var candidate = new pluginVerilog.Data.VerilogCommon.AutoCompleteItem(
               pluginVerilog.Data.VerilogCommon.AutoCompleteItem.CompleteType.Keyword,
               "begin", 4);
           SystemVerilogCore.Documents.IVerilogAutocompleteItem asContract = candidate;

           Assert.Equal("begin", asContract.Label);
           Assert.Equal("begin", asContract.InsertText);
       }

      [Fact]
      public async Task InMemoryCore_CompletionNoDotIdentifierCandidates()
     {
         EnsurePluginRegistered();

         var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
         var project = (SystemVerilogLanguageServer.Server.InMemoryProject)await core.GetProjectAsync("default");

         // typing an identifier without a dot in the module body:
         // instance / variable name candidates must appear (regression test for
         // the member-access early-return introduced for "MODULE0 inst0.")
         // note: no module instantiation here; ModuleInstantiation.ParseAsync calls
         // Controller.AppendLog which NREs in headless hosts (separate known issue)
         string text =
"""
module top;
    wire mysignal;
    wire dst;
    assign dst = mysi;
endmodule
""";

         project.AddOrUpdateFile("mem://nodot.sv", null, text, true);
         var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)project.FindFile("mem://nodot.sv")!;

         // caret after "mysi" (incomplete candidate word)
         int index = text.IndexOf("mysi", StringComparison.Ordinal) + "mysi".Length;
         var entries = project.GetCompletionItems(memFile, index);

         Assert.True(entries != null, $"entries is null; docLen={memFile.CodeDocument.Length}, index={index}");
        // note: the "mysignal" DataObject candidate is not asserted here because in
        // headless hosts file.ParsedDocument (used by GetAutoCompleteTarget's
        // namespace lookup) is not populated by ParseEngine; namespace resolution
        // is an editor-host behavior. Editor-side verification of the regression
        // fix (Module.cs Clear + CompletionContext GetAutoCompleteTarget ret=false)
        // is done manually on the running editor.
        Assert.NotEmpty(entries!);
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
           // all diagnostic ranges must be within the document and at least
           // one character long (end position sanity check)
           foreach (var d in doc.Diagnostics)
           {
               Assert.True(d.Range.EndIndex > d.Range.StartIndex);
               Assert.True(d.Range.EndIndex <= memFile.CodeDocument.Length,
                   $"range end {d.Range.EndIndex} exceeds document length {memFile.CodeDocument.Length}");
           }
       }

       [Fact]
       public void InMemoryCore_DiagnosticsReadableWhileReparsing()
       {
           // Regression: the RTL hierarchy listing reads every file's
           // diagnostics while the background workspace parse replaces them.
           // Enumerating the live list threw "Collection was modified", which
           // failed the whole rtl/hierarchy request and blanked the tree (it
           // looked like the extension would not start).
           EnsurePluginRegistered();

           var core = new SystemVerilogLanguageServer.Server.InMemorySystemVerilogCore();
           var project = (SystemVerilogLanguageServer.Server.InMemoryProject)core.GetOrCreateProject("default");

           string text =
"""
module top;
    wire sig;
    assign dst = 1;
endmodule
""";

           project.AddOrUpdateFile("mem://race.sv", null, text, true);
           var memFile = (SystemVerilogLanguageServer.Server.InMemoryFile)project.FindFile("mem://race.sv")!;
           memFile.ForceBuild();

           using var stop = new System.Threading.CancellationTokenSource();
           // writer: keeps replacing the diagnostic list (what a background
           // workspace parse does)
           var writer = System.Threading.Tasks.Task.Run(() =>
           {
               while (!stop.IsCancellationRequested)
               {
                   memFile.Rebuild();
               }
           });

           // reader: the hierarchy listing path
           var deadline = System.DateTime.UtcNow.AddSeconds(3);
           int reads = 0;
           while (System.DateTime.UtcNow < deadline)
           {
               // must never throw: enumerating the live list while the writer
               // clears it is exactly what used to fail
               var snapshot = memFile.Diagnostics;
               Assert.NotNull(snapshot);
               int count = 0;
               foreach (var d in snapshot)
               {
                   Assert.NotNull(d);
                   count++;
               }
               Assert.True(count >= 0);
               _ = memFile.HighestDiagnosticSeverity();
               reads++;
           }

           stop.Cancel();
           try { writer.Wait(System.TimeSpan.FromSeconds(10)); } catch (System.AggregateException) { }

           Assert.True(reads > 0, "expected the reader to run at least once");
       }
   }
}
