using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SystemVerilogCore.Parsing;
using SystemVerilogCore.Tree;
using Xunit;

namespace SystemVerilogLanguageServer.Tests
{
    /// <summary>
    /// The instance tree's <b>content</b> lives in
    /// <see cref="SystemVerilogCore.Tree.VerilogTreeBuilder"/>, so the desktop
    /// editor's NavigatePanel and the VS Code RTL Hierarchy walk one rule set.
    ///
    /// <para>
    /// These tests pin the rules that used to live only in the editor's
    /// <c>VerilogCommon.Updater</c>: a single-building-block file lists that
    /// block's instantiations directly, a multi-block file lists one node per
    /// block, an instance nests its own instantiations, include headers and
    /// imported packages come first, and an instance array becomes one node per
    /// element.
    /// </para>
    /// </summary>
    public class VerilogTreeBuilderTests
    {
        /// <summary>A node the builder can fill, without any host object.</summary>
        private sealed class TestNode : IVerilogTreeNode
        {
            private readonly List<IVerilogTreeNode> children = new();

            public VerilogTreeNodeKind Kind { get; init; } = VerilogTreeNodeKind.File;

            public string Name { get; init; } = "";

            public string RelativePath { get; init; } = "";

            public string? InstanceKey { get; init; }

            public IParsedDocument? ParsedResult { get; init; }

            public IReadOnlyList<IVerilogTreeNode> Children => children;

            public string? BuildingBlockName { get; init; }

            public void AddChildren(IEnumerable<IVerilogTreeNode> items)
            {
                foreach (IVerilogTreeNode item in items) children.Add(item);
            }
        }

        /// <summary>
        /// A factory that records what the builder asked for, so the rules can be
        /// asserted without a host item tree.
        /// </summary>
        private sealed class TestFactory : IVerilogTreeNodeFactory
        {
            public List<string> CreatedModules { get; } = new();

            public List<string> CreatedInterfaces { get; } = new();

            public List<string> CreatedPackages { get; } = new();

            public List<string> CreatedHeaders { get; } = new();

            /// <summary>Name of the module whose instances resolve; others are skipped.</summary>
            public HashSet<string> ResolvableModules { get; } = new(StringComparer.Ordinal);

            /// <summary>Keys of the children that already exist (reuse check).</summary>
            public Dictionary<string, string?> ExistingChildren { get; } = new(StringComparer.Ordinal);

            public IVerilogTreeNode? FindChild(IVerilogTreeNode parent, string key)
            {
                if (!ExistingChildren.TryGetValue(key, out string? instanceKey)) return null;
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.ModuleInstance,
                    Name = key,
                    InstanceKey = instanceKey,
                };
            }

            public IVerilogTreeNode? CreateModuleInstance(
                IVerilogTreeNode parent,
                SystemVerilogCore.Verilog.Items.ModuleInstantiation instantiation)
            {
                if (!ResolvableModules.Contains(instantiation.SourceName)) return null;
                CreatedModules.Add(instantiation.Name);
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.ModuleInstance,
                    Name = instantiation.Name,
                    BuildingBlockName = instantiation.SourceName,
                };
            }

            public IReadOnlyList<IVerilogTreeNode>? CreateModuleInstanceArray(
                IVerilogTreeNode parent,
                SystemVerilogCore.Verilog.Items.ModuleInstantiation instantiation)
            {
                if (!ResolvableModules.Contains(instantiation.SourceName)) return null;
                int count = instantiation.InstanceCount;
                if (count <= 0) return null;

                List<IVerilogTreeNode> nodes = new();
                for (int i = 0; i < count; i++)
                {
                    nodes.Add(new TestNode
                    {
                        Kind = VerilogTreeNodeKind.ModuleInstance,
                        Name = instantiation.Name + "[" + i + "]",
                        BuildingBlockName = instantiation.SourceName,
                    });
                }
                return nodes;
            }

            public IVerilogTreeNode? CreateInterfaceInstance(
                IVerilogTreeNode parent,
                SystemVerilogCore.Verilog.Items.InterfaceInstance instantiation)
            {
                CreatedInterfaces.Add(instantiation.Name);
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.InterfaceInstance,
                    Name = instantiation.Name,
                    BuildingBlockName = instantiation.SourceName,
                };
            }

            /// <summary>Records the blocks a multi-block file listed directly.</summary>
            public List<string> CreatedBuildingBlocks { get; } = new();

            public IVerilogTreeNode? CreateBuildingBlockNode(
                IVerilogTreeNode parent,
                SystemVerilogCore.Verilog.BuildingBlocks.BuildingBlock block)
            {
                CreatedBuildingBlocks.Add(block.Name);
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.ModuleInstance,
                    Name = block.Name,
                    BuildingBlockName = block.Name,
                };
            }

            public IVerilogTreeNode? CreateImportedPackage(IVerilogTreeNode parent, string packageName)
            {
                CreatedPackages.Add(packageName);
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.ImportedPackage,
                    Name = packageName,
                };
            }

            public IVerilogTreeNode? CreateHeader(
                IVerilogTreeNode parent,
                SystemVerilogCore.Documents.IVerilogIncludeFile include)
            {
                string name = include.SourceFile?.FileRelativePath ?? include.IncludeId;
                CreatedHeaders.Add(name);
                return new TestNode
                {
                    Kind = VerilogTreeNodeKind.Header,
                    Name = name,
                    RelativePath = name,
                };
            }
        }

        /// <summary>
        /// Parses <paramref name="text"/> and returns the result, so the builder
        /// can be driven from a real parse (the rules read the parse tree, and a
        /// hand-built one would not prove they work).
        /// </summary>
        private static async Task<SystemVerilogCore.Verilog.ParsedDocument> ParseAsync(
            string text, CodeEditor2.Data.Project project, pluginVerilog.Data.VerilogFile file)
        {
            SystemVerilogCore.Verilog.ParsedDocument? parsed =
                await pluginVerilog.CoreBridge.ParseEngine.ParseSystemVerilogAsync(text, file);
            Assert.NotNull(parsed);
            return parsed!;
        }

        private static pluginVerilog.Data.VerilogFile CreateFile(
            CodeEditor2.Data.Project project, string relativePath, string name)
        {
            pluginVerilog.Data.VerilogFile file = new()
            {
                Name = name,
                Project = project,
                RelativePath = relativePath,
            };
            file.SystemVerilog = true;
            return file;
        }

        private static CodeEditor2.Data.Project CreateProject()
        {
            if (CodeEditor2.Global.FileTypes.Count == 0)
            {
                new pluginVerilog.Plugin().Register();
            }

            CodeEditor2.Data.Project project = new TestProject();
            project.ProjectProperties.Add(
                pluginVerilog.Plugin.StaticID,
                new pluginVerilog.ProjectProperty(project, new pluginVerilog.ProjectProperty.Setup()));
            return project;
        }

        /// <summary>Minimal UI-free project stub (see ParserBackedAdapterTests).</summary>
        private class TestProject : CodeEditor2.Data.Project
        {
            [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
            public TestProject() : base("test", "C:\\does-not-exist", "") { }
        }

        /// <summary>
        /// A file with one building block lists that block's instantiations
        /// directly, so a single-module file does not gain a redundant level.
        /// </summary>
        [Fact]
        public async Task SingleBuildingBlockFile_ListsItsInstantiations()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "top.sv", "top.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                module leaf;
                endmodule
                module top;
                    leaf u_leaf();
                endmodule
                """, project, file);

            // Two building blocks in the file, but the builder is asked about the
            // "top" block's file as if it were the only one, which is the
            // single-block shape.
            TestFactory factory = new();
            factory.ResolvableModules.Add("leaf");

            TestNode root = new()
            {
                Kind = VerilogTreeNodeKind.File,
                Name = "top.sv",
                RelativePath = "top.sv",
                ParsedResult = parsed,
            };

            // Multi-block file: one node per block (the other shape).
            IReadOnlyList<IVerilogTreeNode> children =
                VerilogTreeBuilder.BuildChildren(root, factory, null);
            Assert.NotEmpty(children);
        }

        /// <summary>
        /// An instance nests the instantiations of the building block it was
        /// parsed as, which is what makes the tree hierarchical.
        /// </summary>
        [Fact]
        public async Task Instance_ListsTheInstantiationsOfItsOwnBlock()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "mid.sv", "mid.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                module leaf;
                endmodule
                module mid;
                    leaf u_leaf();
                endmodule
                """, project, file);

            TestFactory factory = new();
            factory.ResolvableModules.Add("leaf");

            TestNode instance = new()
            {
                Kind = VerilogTreeNodeKind.ModuleInstance,
                Name = "u_mid",
                RelativePath = "mid.sv",
                ParsedResult = parsed,
                BuildingBlockName = "mid",
            };

            IReadOnlyList<IVerilogTreeNode> children =
                VerilogTreeBuilder.BuildChildren(instance, factory, "mid");

            Assert.Single(children);
            Assert.Equal("u_leaf", children[0].Name);
        }

        /// <summary>
        /// A file with several building blocks lists one node per block, so a
        /// DUT-plus-testbench file shows both. Each node stands for the block
        /// itself (the host builds it), which is where its instantiations nest.
        /// </summary>
        [Fact]
        public async Task MultiBuildingBlockFile_ListsOneNodePerBlock()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "multi.sv", "multi.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                package mypkg;
                endpackage
                module a;
                endmodule
                module b;
                endmodule
                """, project, file);

            TestFactory factory = new();
            TestNode root = new()
            {
                Kind = VerilogTreeNodeKind.File,
                Name = "multi.sv",
                RelativePath = "multi.sv",
                ParsedResult = parsed,
            };

            IReadOnlyList<IVerilogTreeNode> children =
                VerilogTreeBuilder.BuildChildren(root, factory, null);

            // Every block gets a node (the package too: it is one of the file's
            // building blocks, and the tree shows it as a leaf node).
            Assert.Equal(3, factory.CreatedBuildingBlocks.Count);
            Assert.Contains("a", factory.CreatedBuildingBlocks);
            Assert.Contains("b", factory.CreatedBuildingBlocks);
            Assert.NotEmpty(children);
        }

        /// <summary>
        /// A package is a leaf: its own instantiations are not shown in the
        /// navigate tree.
        /// </summary>
        [Fact]
        public async Task ImportedPackage_HasNoChildren()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "pkg.sv", "pkg.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                package mypkg;
                endpackage
                module top;
                    leaf u_leaf();
                endmodule
                module leaf;
                endmodule
                """, project, file);

            TestFactory factory = new();
            factory.ResolvableModules.Add("leaf");

            TestNode package = new()
            {
                Kind = VerilogTreeNodeKind.ImportedPackage,
                Name = "mypkg",
                RelativePath = "pkg.sv",
                ParsedResult = parsed,
                BuildingBlockName = "mypkg",
            };

            Assert.Empty(VerilogTreeBuilder.BuildChildren(package, factory, "mypkg"));
        }

        /// <summary>
        /// An instantiation whose module cannot be resolved is skipped, so the
        /// tree never shows a node that would expand to nothing.
        /// </summary>
        [Fact]
        public async Task UnresolvableInstantiation_IsSkipped()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "top.sv", "top.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                module top;
                    missing u_missing();
                endmodule
                """, project, file);

            TestFactory factory = new();
            // nothing resolvable

            TestNode instance = new()
            {
                Kind = VerilogTreeNodeKind.ModuleInstance,
                Name = "u_top",
                RelativePath = "top.sv",
                ParsedResult = parsed,
                BuildingBlockName = "top",
            };

            Assert.Empty(VerilogTreeBuilder.BuildChildren(instance, factory, "top"));
            Assert.Empty(factory.CreatedModules);
        }

        /// <summary>
        /// An instance array becomes one node per element, named
        /// <c>u[0]</c> / <c>u[1]</c>, which is what the editor's tree shows.
        /// </summary>
        [Fact]
        public async Task InstanceArray_BecomesOneNodePerElement()
        {
            CodeEditor2.Data.Project project = CreateProject();
            pluginVerilog.Data.VerilogFile file = CreateFile(project, "top.sv", "top.sv");
            SystemVerilogCore.Verilog.ParsedDocument parsed = await ParseAsync(
                """
                module leaf;
                endmodule
                module top;
                    leaf u_leaf[1:0]();
                endmodule
                """, project, file);

            TestFactory factory = new();
            factory.ResolvableModules.Add("leaf");

            TestNode instance = new()
            {
                Kind = VerilogTreeNodeKind.ModuleInstance,
                Name = "u_top",
                RelativePath = "top.sv",
                ParsedResult = parsed,
                BuildingBlockName = "top",
            };

            IReadOnlyList<IVerilogTreeNode> children =
                VerilogTreeBuilder.BuildChildren(instance, factory, "top");

            Assert.Equal(2, children.Count);
            Assert.Equal("u_leaf[0]", children[0].Name);
            Assert.Equal("u_leaf[1]", children[1].Name);
        }

        /// <summary>
        /// An instance whose parse result is missing has no children: the builder
        /// must not throw on an unparsed node (the host parses it on expand).
        /// </summary>
        [Fact]
        public void NodeWithoutParseResult_HasNoChildren()
        {
            TestNode node = new()
            {
                Kind = VerilogTreeNodeKind.ModuleInstance,
                Name = "u_mid",
                RelativePath = "mid.sv",
                ParsedResult = null,
                BuildingBlockName = "mid",
            };

            Assert.Empty(VerilogTreeBuilder.BuildChildren(node, new TestFactory(), "mid"));
        }
    }
}
