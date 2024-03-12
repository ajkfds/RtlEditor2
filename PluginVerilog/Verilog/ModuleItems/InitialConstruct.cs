using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public class InitialConstruct
    {
        protected InitialConstruct() { }
        public Statements.IStatement Statetment { get; protected set; }

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            ModuleItems.InitialConstruct initial = ModuleItems.InitialConstruct.ParseCreate(word, nameSpace);
            return true;
        }

        public static InitialConstruct ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            //    initial_construct   ::= initial statement
            System.Diagnostics.Debug.Assert(word.Text == "initial");
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            InitialConstruct initial = new InitialConstruct();
            initial.Statetment = Statements.Statements.ParseCreateStatement(word, nameSpace);
            if (initial.Statetment == null)
            {
                word.AddError("illegal initial construct");
                return null;
            }
            return initial;
        }
    }
}
