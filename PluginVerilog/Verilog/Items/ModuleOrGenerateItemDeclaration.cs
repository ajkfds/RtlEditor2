using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Items
{
    public class ModuleOrGenerateItemDeclaration
    {
        /*
        ## SystemVerilog 2012

        module_or_generate_item_declaration ::= 
              package_or_generate_item_declaration 
            | genvar_declaration 
            | clocking_declaration 
            | default clocking clocking_identifier ;
        */

        public static bool Parse(WordScanner word, BuildingBlocks.BuildingBlock buildingBlock)
        {
            // package_or_generate_item_declaration
            if (PackageOrGenerateItemDeclaration.Parse(word, buildingBlock)) return true;

            switch (word.Text)
            {
                // genvar_declaration
                case "genvar":
                    DataObjects.Variables.Genvar.ParseCreateFromDeclaration(word, buildingBlock);
                    return true;
                // clocking_declaration ::= [ default ] clocking [ clocking_identifier ] clocking_event ; { clocking_item } endclocking  [ : clocking_identifier]  | global clocking[clocking_identifier] clocking_event; endclocking[ : clocking_identifier]
                // "default clocking" clocking_identifier;
                case "clocking":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    word.AddError("not implemented");
                    word.SkipToKeyword(";");
                    return true;
                case "default":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    if(word.Text == "clocking")
                    {
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                        if (General.IsIdentifier(word.Text))
                        {
                            word.Color(CodeDrawStyle.ColorType.Identifier);
                            word.MoveNext();
                        }
                        else
                        {
                            word.AddError("illegal identifier");
                            word.SkipToKeyword(";");
                        }
                    }
                    else
                    {
                        word.AddError("clocking expected");
                    }
                    return true;
                default:
                    return false;
            }



        }

    }
}
