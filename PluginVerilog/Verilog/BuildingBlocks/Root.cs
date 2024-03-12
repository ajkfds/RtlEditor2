using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.BuildingBlocks
{

    /* Verilog 2001
        source_text ::= { description }
        description ::= module_declaration
                        | udp_declaration
        module_declaration ::=      { attribute_instance } module_keyword module_identifier [ module_parameter_port_list ]
                                    [ list_of_ports ] ; { module_item }
                                    endmodule

                                    | { attribute_instance } module_keyword module_identifier [ module_parameter_port_list ]
                                    [ list_of_port_declarations ] ; { non_port_module_item }
                                    endmodule
        module_keyword ::= module | macromodule
    */

    /* System Verilog 2012
    source_text ::= [ timeunits_declaration ] { description } 
    description ::= 
          module_declaration 
        | udp_declaration 
        | interface_declaration 
        | program_declaration         
        | package_declaration 
        | { attribute_instance } package_item 
        | { attribute_instance } bind_directive 
        | config_declaration 

    module_nonansi_header ::= 
        { attribute_instance } module_keyword [ lifetime ] module_identifier 
        { package_import_declaration } [ parameter_port_list ] list_of_ports ;

    module_ansi_header ::= 
        { attribute_instance } module_keyword [ lifetime ] module_identifier 
        { package_import_declaration }1 [ parameter_port_list ] [ list_of_port_declarations ] ;

    module_declaration ::= 
            module_nonansi_header [ timeunits_declaration ] { module_item } 
            endmodule [ : module_identifier ] 

            | module_ansi_header [ timeunits_declaration ] { non_port_module_item } 
            endmodule [ : module_identifier ] 

            | { attribute_instance } module_keyword [ lifetime ] module_identifier ( .* ) ;
            [ timeunits_declaration ] { module_item } endmodule [ : module_identifier ] 

            | extern module_nonansi_header 
            | extern module_ansi_header 
    module_keyword ::= module | macromodule

    */
    public class Root : BuildingBlocks.BuildingBlock
    {
        protected Root() : base(null, null)
        {

        }

        public Dictionary<string, BuildingBlock> BuldingBlocks = new Dictionary<string, BuildingBlock>();

        public static Root ParseCreate(WordScanner word, ParsedDocument parsedDocument,Data.VerilogFile file)
        {
            Root root = new Root();
            root.BuildingBlock = root;
            parsedDocument.Root = root;

            while (!word.Eof)
            {
                switch (word.Text)
                {
                    // module_declaration
                    case "module":
                    case "macromodule":
                        parseModule(word, parsedDocument, file);
                        break;
                    // udp_declaration
                    // interface_declaration
                    case "interface":
                        parseInterface(word, parsedDocument, file);
                        break;
                    // program_declaration
                    // bind_directive
                    // config_declaration
                    // package_declaration
                    default:
                        // package_item
                        if (!Items.PackageItem.Parse(word, root))
                        {
                            word.MoveNext();
                        }
                        break;

                }
            }

            return root;
        }


        private static void parseModule(WordScanner word, ParsedDocument parsedDocument, Data.VerilogFile file)
        {
            if (word.Text != "module" && word.Text != "macromodule") System.Diagnostics.Debugger.Break();

            if (parsedDocument.TargetBuldingBlockName != null)
            {
                string moduleName = word.NextText;
                if (moduleName != parsedDocument.TargetBuldingBlockName)
                {
                    word.SkipToKeyword("endmodule");
                    word.MoveNext();
                    return;
                }
            }


            Module module;
            IndexReference iref = IndexReference.Create(parsedDocument);

            if (parsedDocument.ParseMode == Parser.VerilogParser.ParseModeEnum.LoadParse)
            {
                if (parsedDocument.ParameterOverrides == null)
                {
                    module = Module.Create(word, null, file, true);
                }
                else
                {
                    module = Module.Create(word, parsedDocument.ParameterOverrides, null , file, true);
                }
                if (module.Instantiations.Count != 0) // prepare reparse (instanced module could have un-refferenced link)
                {
                    module.ReperseRequested = true;
                }
            }
            else
            {
                if (parsedDocument.ParameterOverrides == null)
                {
                    module = Module.Create(word, null, file, false);
                }
                else
                {
                    module = Module.Create(word, parsedDocument.ParameterOverrides, null, file, false);
                }
            }

            if (!parsedDocument.Root.BuldingBlocks.ContainsKey(module.Name))
            {
                parsedDocument.Root.BuldingBlocks.Add(module.Name, module);
                if (module.ReperseRequested) parsedDocument.ReparseRequested = true;
            }
            else
            {
                word.AddError("duplicated module name");
            }
        }

        private static void parseInterface(WordScanner word, ParsedDocument parsedDocument, Data.VerilogFile file)
        {
            if (word.Text != "interface") System.Diagnostics.Debugger.Break();

            if (parsedDocument.TargetBuldingBlockName != null)
            {
                string moduleName = word.NextText;
                if (moduleName != parsedDocument.TargetBuldingBlockName)
                {
                    word.SkipToKeyword("endinterface");
                    word.MoveNext();
                    return;
                }
            }

            Interface module;
            IndexReference iref = IndexReference.Create(parsedDocument);

            if (parsedDocument.ParseMode == Parser.VerilogParser.ParseModeEnum.LoadParse)
            {
                if (parsedDocument.ParameterOverrides == null)
                {
                    module = Interface.Create(word, null, file, true);
                }
                else
                {
                    module = Interface.Create(word, parsedDocument.ParameterOverrides, null, file, true);
                }
                if (module.Instantiations.Count != 0) // prepare reparse (instanced module could have un-refferenced link)
                {
                    module.ReperseRequested = true;
                }
            }
            else
            {
                if (parsedDocument.ParameterOverrides == null)
                {
                    module = Interface.Create(word, null, file, false);
                }
                else
                {
                    module = Interface.Create(word, parsedDocument.ParameterOverrides, null, file, false);
                }
            }

            if (!parsedDocument.Root.BuldingBlocks.ContainsKey(module.Name))
            {
                parsedDocument.Root.BuldingBlocks.Add(module.Name, module);
                if (module.ReperseRequested) parsedDocument.ReparseRequested = true;
            }
            else
            {
                word.AddError("duplicated module name");
            }
        }

    }
}
