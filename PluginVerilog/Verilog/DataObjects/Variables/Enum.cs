using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ajkControls;
using ajkControls.ColorLabel;
using pluginVerilog.Verilog.DataObjects;
using pluginVerilog.Verilog.DataObjects.DataTypes;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    /*
    type_declaration::= 
        typedef data_type type_identifier { variable_dimension};
        | typedef interface_instance_identifier constant_bit_select.type_identifier type_identifier;
        | typedef[enum | struct | union | class | interface class ] type_identifier ;


        data_type ::=
            ... 
            | "enum" [ enum_base_type ] { enum_name_declaration { , enum_name_declaration } } { packed_dimension } 
            ... 

        enum_base_type ::= 
            integer_atom_type [ signing ] 
            | integer_vector_type [ signing ] [ packed_dimension ] 
            | type_identifier [ packed_dimension ]

        enum_name_declaration ::= 
            enum_identifier [ [ integral_number [ : integral_number ] ] ] [ = constant_expression ]

        integer_type            ::= integer_vector_type | integer_atom_type 
        integer_atom_type       ::= byte | shortint | int | longint | integer | time
        integer_vector_type     ::= bit | logic | reg

        non_integer_type ::= shortreal | real | realtime

    */
    public class Enum : Variable
    {
        protected Enum() { }

        public List<Range> PackedDimensions { get; set; } = new List<Range>();

        public DataType BaseType { get; protected set; } = null;
        public List<DataTypes.Enum.Item> Items = new List<DataTypes.Enum.Item>();

        public Range Range {
            get
            {
                if (PackedDimensions.Count < 1) return null;
                return PackedDimensions[0];
            }
        } 


        public override void AppendLabel(ColorLabel label)
        {
            AppendTypeLabel(label);
            label.AppendText(Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Register));

            foreach (Range dimension in Dimensions)
            {
                label.AppendText(" ");
                label.AppendLabel(dimension.GetLabel());
            }

            if (Comment != "")
            {
                label.AppendText(" ");
                label.AppendText(Comment.Trim(new char[] { '\r', '\n', '\t', ' ' }), Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Comment));
            }

            label.AppendText("\r\n");
        }

        public override void AppendTypeLabel(ColorLabel label)
        {
        }

        public static new Enum Create(DataTypes.DataType dataType)
        {
            System.Diagnostics.Debug.Assert(dataType.Type == DataTypes.DataTypeEnum.Enum);
            DataTypes.Enum dType = dataType as DataTypes.Enum;

            Enum val = new Enum();
            val.DataType = dType.Type;
            foreach(var item in dType.Items)
            {
                val.Items.Add(item);
            }
            return val;
        }



    }
}

