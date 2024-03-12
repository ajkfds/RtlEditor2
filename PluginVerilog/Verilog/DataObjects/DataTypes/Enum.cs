using pluginVerilog.Verilog.DataObjects.Constants;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes
{
    public class Enum : DataType
    {
        public override DataTypeEnum Type
        {
            get
            {
                return DataTypeEnum.Enum;
            }
        }

        public DataType BaseType { get; protected set; } = null;
        public List<Item> Items = new List<Item>();

        public static Enum ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "enum") System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.AddSystemVerilogError();
            word.MoveNext();

            Enum type = new Enum();

            // baseType
            switch (word.Text)
            {
                //integer_vector_type::= bit | logic | reg
                case "bit":
                case "logic":
                case "reg":
                    type.BaseType = IntegerVectorType.ParseCreate(word, nameSpace);
                    break;
                //integer_atom_type::= byte | shortint | int | longint | integer | time
                case "byte":
                case "shortint":
                case "int":
                case "longint":
                case "integer":
                case "time":
                    type.BaseType =  IntegerAtomType.ParseCreate(word, nameSpace);
                    break;
                default:
                    // In the absence of a data type declaration, the default data type shall be int.
                    // Any other data type used with enumerated types shall require an explicit data type declaration.
                    type.BaseType = new DataTypes.IntegerAtomType();
                    type.BaseType.Type = DataTypeEnum.Int;
                    break;
            }

            if(word.Eof | word.Text != "{")
            {
                word.AddError("{ required");
                return null;
            }
            word.MoveNext(); // "{"

            int index = 0;
            while( !word.Eof | word.Text != "}")
            {
                if (!parseItem(type, word, nameSpace, ref index)) break;

                if (word.Text == ",")
                {
                    word.MoveNext();
                    if (word.Text == "}") word.AddError("illegal comma");
                }
            }

            if (word.Eof | word.Text != "}")
            {
                word.AddError("{ required");
                return null;
            }
            word.MoveNext();

            return type;
        }

        private static bool parseItem(Enum enum_,WordScanner word, NameSpace nameSpace, ref int index)
        {
            /*
            enum_name_declaration::=
                enum_identifier[ [integral_number[ : integral_number]] ] [ = constant_expression ]
            */
            if (word.Text == "}" | word.Text == ",") return false;
            if (!General.IsIdentifier(word.Text)) return false;

            Item item = new Item();
            item.Identifier = word.Text;
            word.Color(CodeDrawStyle.ColorType.Paramater);

            EnumConstants constants = EnumConstants.Create(enum_.BaseType);
            constants.Name = item.Identifier;
            constants.DefinedReference = word.GetReference();
            if (!nameSpace.Constants.ContainsKey(constants.Name)) nameSpace.Constants.Add(constants.Name, constants);

            word.MoveNext();

            Range range = null;
            if (word.Text == "[")
            {
                range = Range.ParseCreate(word, nameSpace);
            }

            Expressions.Expression exp = null;
            if (word.Text == "=")
            {
                word.MoveNext();    // =
                exp = Expressions.Expression.ParseCreate(word, nameSpace);
            }

            if (exp != null)
            {
                int.TryParse(exp.ConstantValueString(), out index);
            }
            item.Index = index;

            enum_.Items.Add(item);

            index++;
            return true;
        }

        public class Item
        {
            public string Identifier;
            public int Index;
        }

    }
}
