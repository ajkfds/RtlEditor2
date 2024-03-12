using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls;
using ajkControls.Json;
using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.ModuleItems;

namespace pluginVerilog
{
    public class ProjectProperty : codeEditor.Data.ProjectProperty
    {
        public ProjectProperty(codeEditor.Data.Project project)
        {
            this.project = project;
        }
        private codeEditor.Data.Project project;

        public Verilog.AutoComplete.Setup SnippetSetup = new Verilog.AutoComplete.Setup();

        public override void SaveSetup(ajkControls.Json.JsonWriter writer)
        {
            using (var macroWriter = writer.GetObjectWriter("Macros"))
            {
                foreach (var kvp in Macros)
                {
                    macroWriter.writeKeyValue(kvp.Key, kvp.Value.MacroText);
                }
            }
        }

        // VerilogModuleParsedData
        public override void LoadSetup(JsonReader jsonReader)
        {
            Macros.Clear();
            using(var reader = jsonReader.GetNextObjectReader())
            {
                while (true)
                {
                    string key = reader.GetNextKey();
                    if (key == null) break;

                    switch (key)
                    {
                        case "Macros":
                            loadMacros(reader);
                            break;
                        default:
                            reader.SkipValue();
                            break;
                    }
                }
            }
        }

        public BuildingBlock GetInstancedBuildingBlock(IInstantiation instantiation)
        {
            if (instantiation.ParameterOverrides.Count == 0)
            {
                return GetBuildingBlock(instantiation.SourceName);
            }
            else
            {
                Data.VerilogFile file = GetFileOfBuildingblock(instantiation.SourceName) as Data.VerilogFile;
                if (file == null) return null;
                Verilog.ParsedDocument parsedDocument = file.GetInstancedParsedDocument(instantiation.SourceName + ":" + instantiation.OverrideParameterID) as Verilog.ParsedDocument;
                if (parsedDocument == null) return null;
                if (!parsedDocument.Root.BuldingBlocks.ContainsKey(instantiation.SourceName)) return null;
                return parsedDocument.Root.BuldingBlocks[instantiation.SourceName];
            }
        }

        public void loadMacros(ajkControls.Json.JsonReader jsonReader)
        {
            using(var reader = jsonReader.GetNextObjectReader())
            {
                while (true)
                {
                    string macroIdentifier = reader.GetNextKey();
                    if (macroIdentifier == null) break;
                    if (Macros.ContainsKey(macroIdentifier))
                    {
                        reader.SkipValue();
                    }
                    else
                    {
                        string macroText = reader.GetNextStringValue();
                        Macros.Add(macroIdentifier, Verilog.Macro.Create(macroIdentifier, macroText));
                    }
                }
            }

        }

        private Dictionary<string, codeEditor.CodeEditor.ParsedDocument> pdocs = new Dictionary<string, codeEditor.CodeEditor.ParsedDocument>();
        public IReadOnlyDictionary<string, codeEditor.CodeEditor.ParsedDocument> VerilogModuleInstanceParsedDocuments
        {
            get
            {
                return pdocs;
            }
        }
        public void RegisterParsedDocument(string ID,codeEditor.CodeEditor.ParsedDocument parsedDocument)
        {
            if (pdocs.ContainsKey(ID))
            {
                pdocs.Remove(ID);
            }
            pdocs.Add(ID, parsedDocument);
        }

        public void RemoveRegisteredParsedDocument(string ID,codeEditor.CodeEditor.ParsedDocument parsedDocument)
        {
            if (pdocs.ContainsKey(ID))
            {
                pdocs.Remove(ID);
            }
            else
            {
                System.Diagnostics.Debugger.Break();
            }
        }

        public bool IsRegisteredParsedDocument(string ID)
        {
            if (pdocs.ContainsKey(ID))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public codeEditor.CodeEditor.ParsedDocument GetParsedDocument(string id)
        {
            if (id == null) System.Diagnostics.Debugger.Break();

            if (pdocs.ContainsKey(id))
            {
                return pdocs[id];
            }
            else
            {
                return null;
            }
        }


        // module reference table
        private Dictionary<string, System.WeakReference<Data.IVerilogRelatedFile>> moduleFileRefs = new Dictionary<string, WeakReference<Data.IVerilogRelatedFile>>();

        public bool RegisterModule(string moduleName,Data.IVerilogRelatedFile file)
        {
            lock (moduleFileRefs)
            {
                if (moduleFileRefs.ContainsKey(moduleName))
                {
                    Data.IVerilogRelatedFile prevFile;
                    if(!moduleFileRefs[moduleName].TryGetTarget(out prevFile))
                    {
                        moduleFileRefs[moduleName] = new WeakReference<Data.IVerilogRelatedFile>(file);
                        return true;
                    }
                    else
                    {
                        if(prevFile == file)
                        {
                            System.Diagnostics.Debugger.Break(); // duplicated register
                            return true;
                        }
                        // other taeget registered
                        return false;
                    }
                }
                else
                {
                    moduleFileRefs.Add(moduleName, new WeakReference<Data.IVerilogRelatedFile>(file));
                    return true;
                }
            }
        }

        public bool RemoveModule(string moduleName, Data.IVerilogRelatedFile file)
        {
            lock (moduleFileRefs)
            {
                if (moduleFileRefs.ContainsKey(moduleName))
                {
                    Data.IVerilogRelatedFile prevFile;
                    if (!moduleFileRefs[moduleName].TryGetTarget(out prevFile))
                    {
                        System.Diagnostics.Debugger.Break(); // already disposed
                        moduleFileRefs.Remove(moduleName);
                        return true;
                    }
                    else
                    {
                        if (prevFile == file)
                        {
                            moduleFileRefs.Remove(moduleName);
                            return true;
                        }
                        // unmatch target file
                        return false;
                    }
                }
                else
                {
                    // no target file
                    return false;
                }
            }
        }

        public bool IsRegisterableModule(string moduleName, Data.IVerilogRelatedFile file)
        {
            lock (moduleFileRefs) 
            {
                if (moduleFileRefs.ContainsKey(moduleName))
                {
                    Data.IVerilogRelatedFile prevFile;
                    if (!moduleFileRefs[moduleName].TryGetTarget(out prevFile))
                    {
                        return true;
                    }
                    else
                    {
                        if (prevFile == file)
                        {
                            return false;
                        }
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        public Data.IVerilogRelatedFile GetFileOfBuildingblock(string buildingBlockName)
        {
            lock (moduleFileRefs)
            {
                if (moduleFileRefs.ContainsKey(buildingBlockName))
                {
                    Data.IVerilogRelatedFile file;
                    if (!moduleFileRefs[buildingBlockName].TryGetTarget(out file))
                    {
                        return null;
                    }
                    else
                    {
                        return file;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public List<string> GetModuleNameList()
        {
            lock (moduleFileRefs)
            {
                return moduleFileRefs.Keys.ToList<string>();
            }
        }

        public BuildingBlock
            GetBuildingBlock(string moduleName)
        {
            Data.IVerilogRelatedFile file = GetFileOfBuildingblock(moduleName);
            if (file == null) return null;

            if (file == null || file.VerilogParsedDocument == null) return null;
            if (!file.VerilogParsedDocument.Root.BuldingBlocks.ContainsKey(moduleName)) return null;
            return file.VerilogParsedDocument.Root.BuldingBlocks[moduleName] as BuildingBlock;
        }

        // inline comment
        public Dictionary<string, Action<Verilog.ParsedDocument>> InLineCommentCommands = new Dictionary<string, Action<Verilog.ParsedDocument>>();

        // macros
        public Dictionary<string, Verilog.Macro> Macros = new Dictionary<string, Verilog.Macro>();

        // system tasks
        public Dictionary<string, Func<Verilog.WordScanner, Verilog.NameSpace, Verilog.Statements.SystemTask.SystemTask>> SystemTaskParsers = new Dictionary<string, Func<Verilog.WordScanner, Verilog.NameSpace, Verilog.Statements.SystemTask.SystemTask>>
        {
            // Display task
            {"$display",null },
            {"$strobe",null },
            {"$displayb",null },
            {"$strobeb",null },
            {"$displayh",null },
            {"$strobeh",null },
            {"$displayo",null },
            {"$strobeo", null },
            {"$monitor", null },
            {"$write", null },
            {"$monitorb", null },
            {"$writeb", null },
            {"$monitorh", null },
            {"$writeh", null },
            {"$monitoro", null },
            {"$writeo", null },
            {"$monitoroff", null },
            {"$monitoron", null },
            // File I/O tasks
            {"$fclose", null },
            {"$fdisplay", null },
            {"$fstrobe", null } ,
            {"$fdisplayb", null },
            {"$fstrobeb", null },
            {"$fdisplayh", null },
            {"$fstrobeh", null },
            {"$fdisplayo", null },
            {"$fstrobeo", null },
            {"$ungetc", null },
            {"$fflush", null },
            {"$fmonitor", null },
            {"$fwrite", null },
            {"$fmonitorb", null },
            {"$fwriteb", null },
            {"$fmonitorh", null },
            {"$fwriteh", null },
            {"$fmonitoro", null },
            {"$fwriteo", null },
            {"$readmemb", null },
            {"$readmemh", null },
            {"$swrite", null },
            {"$swriteb", null },
            {"$swriteo", null },
            {"$swriteh", null } ,
            {"$sdf_annotate", null } ,
            // Timescale tasks
            {"$printtimescale", null },
            {"$timeformat", null },
            // Simulation control tasks
            {"$finish", null },
            {"$stop", null },
            // PLA modeling tasks
            {"$async$and$array", null },
            {"$async$and$plane", null },
            {"$async$nand$array", null },
            {"$async$nand$plane", null },
            {"$async$or$array", null },
            {"$async$or$plane", null },
            {"$async$nor$array", null },
            {"$async$nor$plane", null },
            {"$sync$and$array", null },
            {"$sync$and$plane", null },
            {"$sync$nand$array", null },
            {"$sync$nand$plane", null },
            {"$sync$or$array", null },
            {"$sync$or$plane", null },
            {"$sync$nor$array", null },
            {"$sync$nor$plane", null },
            // Stochastic analysis tasks
            {"$q_initialize", null },
            {"$q_add", null },
            {"$q_remove", null },
            {"$q_full", null },
            {"$q_exam", null },

            // Dump
            {"$dumpfile", (word,nameSpace) =>{ return Verilog.Statements.SystemTask.SystemTask.ParseCreate(word,nameSpace); } },
            {"$dumpall",null },
            {"$dumpoff",null },
            {"$dumpon",null },
            {"$dumpvars", (word,nameSpace) =>{ return Verilog.Statements.SystemTask.SkipArguments.ParseCreate(word,nameSpace); }  },
            {"$dumpflush",null },
            {"$dumplimit",null },
        };

        public Dictionary<string, Func<Verilog.DataObjects.Variables.Variable, Verilog.WordScanner>> SystemFunctions = new Dictionary<string, Func<Verilog.DataObjects.Variables.Variable, Verilog.WordScanner>>
        {
            {"$sformat", null },
            {"$ferror", null },
            {"$rewind", null },
            {"$fseek", null },
            {"$fread", null },
            {"$ftell", null } ,
            {"$sscanf", null } ,
            {"$fscanf", null },
            {"$fgetc", null },
            {"$fgets", null },
            {"$fopen", null },

            // Simulation time functions
            {"$realtime",null },
            {"$stime",null },
            {"$time",null },

            // Conversion functions
            {"$bitstoreal",null },
            {"$realtobits",null },
            {"$itor",null },
            {"$rtoi",null },
            {"$signed",null },
            {"$unsigned",null },

            // Probabilistic distribution functions
            {"$dist_chi_square",null },
            {"$dist_erlang",null },
            {"$dist_exponential",null },
            {"$dist_normal",null },
            {"$dist_poisson",null },
            {"$dist_t",null },
            {"$dist_uniform",null },
            {"$random",null },

            // Command line input
            {"$test$plusargs",null },
            {"$value$plusargs",null },

            // systemverilog
            {"$clog2",null },

        };

        public Dictionary<string, Action<Verilog.WordScanner>> InCommentTags = new Dictionary<string, Action<Verilog.WordScanner>>
        {
            { "@section",null }
        };

    }
}
