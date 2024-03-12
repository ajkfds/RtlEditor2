using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public class AlwaysConstruct
    {
        protected AlwaysConstruct() { }
        public Statements.IStatement Statetment { get; protected set;}

        /* ## Verilog2001
            always_construct ::= always statement     */

        /* ## SystemVeriloh
            always_construct ::= always_keyword statement 
            always_keyword ::= always | always_comb | always_latch | always_ff     
         */

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            ModuleItems.AlwaysConstruct always = ModuleItems.AlwaysConstruct.ParseCreate(word, nameSpace);
            return true;
        }
        public static AlwaysConstruct ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                case "always":
                    break;
                case "always_comb":
                case "always_latch":
                case "always_ff":
                    if (!word.SystemVerilog) word.AddError("Systemverilog Function");
                    break;
                default:
                    System.Diagnostics.Debug.Assert(true);
                    break;
            }


            //System.Diagnostics.Debug.Assert(word.Text == "always");
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            AlwaysConstruct always = new AlwaysConstruct();
            always.Statetment = Statements.Statements.ParseCreateStatement(word, nameSpace);
            if(always.Statetment == null)
            {
                word.AddError("illegal always construct");
                return null;
            }
            return always;
        }
    }

}
