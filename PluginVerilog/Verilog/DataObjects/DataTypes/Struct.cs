using pluginVerilog.Verilog.DataObjects.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes
{
    public class Struct : DataType
    { 
        public override DataTypeEnum Type
        {
            get
            {
                return DataTypeEnum.Struct;
            }
        }

        public bool Tagged = false; 
        public bool Packed = false;
        public bool Signed = false;

        public List<Member> Members = new List<Member>();

        /*
        data_type ::= // from A.2.2.1
        ... 
        | struct_union [ packed [ signing ] ] "{" struct_union_member { struct_union_member } "}" { packed_dimension }

        struct_union_member ::=   { attribute_instance } [random_qualifier] data_type_or_void list_of_variable_decl_assignments ;
        data_type_or_void ::= data_type | "void"
        struct_union ::= "struct" | "union" [ "tagged" ]          
         */

        public static Struct ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "struct" ) System.Diagnostics.Debugger.Break();
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.AddSystemVerilogError();
            word.MoveNext();

            Struct type = new Struct();

            if(word.Text == "tagged")
            {
                type.Tagged = true;
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }

            if (word.Text == "packed")
            {
                type.Packed = true;
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
                switch (word.Text)
                {
                    case "signed":
                        type.Signed = true;
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                        break;
                    case "unsigned":
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                        break;
                }
            }

            if (word.Eof | word.Text != "{")
            {
                word.AddError("{ required");
                return null;
            }
            word.MoveNext(); // "{"

            int index = 0;
            while (!word.Eof | word.Text != "}")
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

        private static bool parseItem(Struct enum_, WordScanner word, NameSpace nameSpace, ref int index)
        {
            /*
            enum_name_declaration::=
                enum_identifier[ [integral_number[ : integral_number]] ] [ = constant_expression ]
            */
            if (word.Text == "}" | word.Text == ",") return false;
            if (!General.IsIdentifier(word.Text)) return false;

            Member item = new Member();
            item.Identifier = word.Text;
            word.Color(CodeDrawStyle.ColorType.Paramater);

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

            enum_.Members.Add(item);

            index++;
            return true;
        }

        public class Member
        {
            public string Identifier;
            public int Index;
        }

    }
}

