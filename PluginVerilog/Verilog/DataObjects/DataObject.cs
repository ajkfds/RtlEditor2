using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects
{
    public class DataObject
    {
        // #SystemVeriog 2017
        //	net												user-defined-size	4state	v
        //
        //	variable	+ integer_vector_type	+ bit 		user-defined-size	2state	sv
        //										+ logic		user-defined-size	4state  sv
        //										+ reg		user-defined-size	4state	v
        //
        //				+ integer_atom_type		+ byte		8bit signed			2state  sv
        //										+ shortint	16bit signed		2state  sv
        //										+ int		32bit signed		2state  sv
        //										+ longint	64bit signed		2state  sv
        //										+ integer	32bit signed		4state	v
        //										+ time		64bit unsigned		        v
        //
        //            	+ non_integer_type		+ shortreal	                            sv
        //										+ real		                            v
        //										+ realtime	                            v
        //              + struct/union
        //              + enum
        //              + string
        //              + chandle
        //              + virtual(interface)
        //              + class/package
        //              + event
        //              + pos_covergroup
        //              + type_reference
        // 


        public string Name { set; get; }
        public string Comment { set; get; } = "";
        public WordReference DefinedReference { set; get; } = null;
        public DataTypeEnum DataType = DataTypeEnum.Reg;
        public List<Range> Dimensions { get; set; } = new List<Range>();

        public List<WordReference> UsedReferences { set; get; } = new List<WordReference>();
        public List<WordReference> AssignedReferences { set; get; } = new List<WordReference>();
        public int DisposedIndex = -1;

        public virtual void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Name);
        }

        public virtual void AppendTypeLabel(ajkControls.ColorLabel.ColorLabel label)
        {

        }

        public virtual DataObject Clone()
        {
            return null;
        }




    }
}
