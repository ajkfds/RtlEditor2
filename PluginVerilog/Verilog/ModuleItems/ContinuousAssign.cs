using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public class ContinuousAssign
    {
        protected ContinuousAssign() { }
        public DriveStrength DriveStrength;
        public Delay3 Delay3;

        public DataObjects.VariableAssignment VariableAssignment { get; protected set; }

        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            ModuleItems.ContinuousAssign continuousAssign = ModuleItems.ContinuousAssign.ParseCreate(word, nameSpace);
            return true;
        }

        public static ContinuousAssign ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            // continuous_assign::= assign[drive_strength][delay3] list_of_net_assignments;
            // list_of_net_assignments::= net_assignment { , net_assignment }
            // net_assignment::= net_lvalue = expression
            if(word.Text != "assign")
            {
                System.Diagnostics.Debugger.Break();
            }
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            ContinuousAssign continuousAssign = new ContinuousAssign();

            continuousAssign.DriveStrength = DriveStrength.ParseCreate(word, nameSpace);
            continuousAssign.Delay3 = Delay3.ParseCreate(word, nameSpace);

            DataObjects.VariableAssignment assignment = DataObjects.VariableAssignment.ParseCreate(word, nameSpace);
            if(assignment != null)
            {
                continuousAssign.VariableAssignment = assignment;
            }
            else
            {
                word.AddError("illegal assignment");
            }


            if (word.GetCharAt(0) == ';')
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; expected");
                word.SkipToKeyword(";");
                word.MoveNext();
            }
            return continuousAssign;
        }
    }
}
