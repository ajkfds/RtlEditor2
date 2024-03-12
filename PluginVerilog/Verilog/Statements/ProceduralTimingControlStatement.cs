using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class ProceduralTimingControlStatement : IStatement
    {
        protected ProceduralTimingControlStatement() { }
        public void DisposeSubReference()
        {
            Statement.DisposeSubReference();
        }
        public DelayControl DelayControl { get; protected set; }
        public EventControl EventControl { get; protected set; }
        public IStatement Statement { get; protected set; }

        public static ProceduralTimingControlStatement ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            switch (word.Text)
            {
                case "#":
                    {
                        ProceduralTimingControlStatement statement = new ProceduralTimingControlStatement();
                        statement.DelayControl = DelayControl.ParseCreate(word, nameSpace);
                        statement.Statement = Statements.ParseCreateStatementOrNull(word, nameSpace);
                        return statement;
                    }
                case "@":
                    {
                        ProceduralTimingControlStatement statement = new ProceduralTimingControlStatement();
                        statement.EventControl = EventControl.ParseCreate(word, nameSpace);
                        statement.Statement = Statements.ParseCreateStatementOrNull(word, nameSpace);
                        return statement;
                    }
                default:
                    return null;
            }
        }
    }

}
