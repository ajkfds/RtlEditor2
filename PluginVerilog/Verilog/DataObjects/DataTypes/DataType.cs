using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.DataTypes

{
    // ## Verilog 2001
    // Variable -+-net +-> wire         4state  >=1bit      v
    //           +-data+-> reg          4state  >=1bit      v
    //                 +-> real                             v
    //                 +-> integer              32bit       v
    //                 +-> event                            v
    //                 +-> genvar                           v
    //                 +-> real                             v
    //                 +-> realtime                         v
    //                 +-> reg                              v
    //                 +-> time                             v
    //                 +-> trireg                           v
    // ## SystemVerilog2012
    // Variable -+-data+-> logic        4state  >=1bit      v
    //                 +-> bit          2state  >=1bit      v
    //                 +-> byte         2state  8bit
    //                 +-> shortint     2state  16bit
    //                 +-> int          2state  32bit
    //                 +-> longint      2state  64bit
    //                 +-> shortreal

    /*
    shortint    2-state data type, 16-bit signed integer 
    int         2-state data type, 32-bit signed integer 
    longint     2-state data type, 64-bit signed integer 
    byte        2-state data type, 8-bit signed integer or ASCII character 
    bit         2-state data type, user-defined vector size, unsigned 
    logic       4-state data type, user-defined vector size, unsigned 
    reg         4-state data type, user-defined vector size, unsigned 
    integer     4-state data type, 32-bit signed integer 
    time        4-state data type, 64-bit unsigned integer
     */

    public enum DataTypeEnum
    {
        //integer_vector_type::= bit | logic | reg
        Bit,
        Logic,
        Reg,
        //integer_atom_type::= byte | shortint | int | longint | integer | time
        Byte,
        Shortint,
        Int,
        Longint,
        Integer,
        Time,
        //non_integer_type::= "shortreal" | "real" | "realtime"
        Shortreal,
        Real,
        Realtime,
        // others
        Enum,
        String,
        Chandle,
        Virtual,
        Class,
        InterfaceInstance,
        Event,
        CoverGroup,
        Struct
        //        TypeReference
    }

    public class DataType
    {
        //        protected List<Variables.Dimension> dimensions = new List<Variables.Dimension>();
        //        public IReadOnlyList<Variables.Dimension> Dimensions { get; }
        //        public virtual int BitWidth { get; }
        //        public virtual bool State4 { get; }
        public virtual DataTypeEnum Type { get; set; }

        /*
        data_type::=
              integer_vector_type[signing] { packed_dimension }
            | integer_atom_type[signing]
            | non_integer_type
            | struct_union["packed"[signing]] { struct_union_member { struct_union_member } } { packed_dimension }
            | "enum" [enum_base_type] { enum_name_declaration { , enum_name_declaration } { packed_dimension }
            | "string"
            | "chandle"
            | "virtual" ["interface"] interface_identifier[parameter_value_assignment][ . modport_identifier] 
            | [class_scope | package_scope] type_identifier { packed_dimension }
            | class_type
            | "event"
            | ps_covergroup_identifier 
            | type_reference

        integer_atom_type   ::= "byte" | "shortint" | "int" | "longint" | "integer" | "time"
        integer_vector_type ::= "bit" | "logic" | "reg"
        non_integer_type    ::= "shortreal" | "real" | "realtime"

        signing ::= "signed" | "unsigned"
        */

        public static DataType ParseCreate(WordScanner word, NameSpace nameSpace, DataTypeEnum? defaultDataType)
        {
            /*
            data_type::=
                  integer_vector_type[signing] { packed_dimension }
                | integer_atom_type[signing]
                | non_integer_type
                | struct_union["packed"[signing]] { struct_union_member { struct_union_member } } { packed_dimension }
                | "enum" [enum_base_type] { enum_name_declaration { , enum_name_declaration } { packed_dimension }
                | "string"
                | "chandle"
                | "virtual" ["interface"] interface_identifier[parameter_value_assignment][ . modport_identifier] 
                | [class_scope | package_scope] type_identifier { packed_dimension }
                | class_type
                | "event"
                | ps_covergroup_identifier 
                | type_reference

            integer_atom_type   ::= "byte" | "shortint" | "int" | "longint" | "integer" | "time"
            integer_vector_type ::= "bit" | "logic" | "reg"
            non_integer_type    ::= "shortreal" | "real" | "realtime"

            signing ::= "signed" | "unsigned"
            */

            // systemverilog data type does not include nets

            switch (word.Text)
            {
                //integer_vector_type::= bit | logic | reg
                case "bit":
                case "logic":
                case "reg":
                    return IntegerVectorType.ParseCreate(word, nameSpace);

                //integer_atom_type::= byte | shortint | int | longint | integer | time
                case "byte":
                case "shortint":
                case "int":
                case "longint":
                case "integer":
                case "time":
                    return IntegerAtomType.ParseCreate(word, nameSpace);
                //non_integer_type::= "shortreal" | "real" | "realtime"
                case "shortreal":
                    return parseSimpleType(word, nameSpace, DataTypeEnum.Shortreal);
                case "real":
                    return parseSimpleType(word, nameSpace, DataTypeEnum.Real);
                case "realtime":
                    return parseSimpleType(word, nameSpace, DataTypeEnum.Realtime);

                // struct_union["packed"[signing]] { struct_union_member { struct_union_member } } { packed_dimension }

                // "enum" [enum_base_type] { enum_name_declaration { , enum_name_declaration } { packed_dimension }
                case "enum":
                    return Enum.ParseCreate(word, nameSpace);

                // "string"
                case "string":
                    return parseSimpleType(word, nameSpace, DataTypeEnum.String);
                // "chandle"
                // "virtual" ["interface"] interface_identifier[parameter_value_assignment][ . modport_identifier] 

                // class_type
                // "event"
                // ps_covergroup_identifier 

                // type_reference
                case "type":
                    return parseTypeReference(word, nameSpace);

                default:
                    // [class_scope | package_scope] type_identifier { packed_dimension }
                    {
                        if (nameSpace.Typedefs.ContainsKey(word.Text))
                        {
                            DataType dType = nameSpace.Typedefs[word.Text].VariableType;
                            if (dType != null)
                            {
                                word.Color(CodeDrawStyle.ColorType.Keyword);
                                word.MoveNext();
                                return dType;
                            }
                        }
                        else if(word.RootParsedDocument.Root != null && word.RootParsedDocument.Root.Typedefs.ContainsKey(word.Text))
                        {
                            DataType dType = word.RootParsedDocument.Root.Typedefs[word.Text].VariableType;
                            if (dType != null)
                            {
                                word.Color(CodeDrawStyle.ColorType.Keyword);
                                word.MoveNext();
                                return dType;
                            }
                        }
                    }

                    if (defaultDataType != null)
                    {
                        // data_type_or_implicit::= data_type | implicit_data_type
                        // signing ::= "signed" | "unsigned"
                        // implicit_data_type::= [signing] { packed_dimension }
                        if (word.Text == "signed" || word.Text == "unsigned" || word.Text == "[")
                        {
                            DataType dType = new DataType();
                            dType.Type = (DataTypeEnum)defaultDataType;
                            return dType;
                        }
                    }

                    break;
            }
            return null;
        }

        private static DataType parseSimpleType(WordScanner word, NameSpace nameSpace, DataTypeEnum dataType)
        {
            DataType dType = new DataType();
            dType.Type = dataType;

            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            return dType;
        }

        private static DataType parseTypeReference(WordScanner word, NameSpace nameSpace)
        {
            //type_reference::= type(expression) | type(data_type)
            if (word.Text != "type") System.Diagnostics.Debugger.Break();

            word.Color(CodeDrawStyle.ColorType.Keyword);
            if (word.Text != "(")
            {
                word.AddError("( required");
                return null;
            }

            DataType dtype = ParseCreate(word, nameSpace,null);
            if (dtype == null)
            {
                Expressions.Expression ex = Expressions.Expression.ParseCreate(word, nameSpace);
                if (ex == null)
                {
                    word.AddError("expression or data_type required");
                    return null;
                }
                else
                {
                    word.AddError("type(expression) is not supported");
                }
            }

            if (word.Text != ")")
            {
                word.AddError(") required");
                return null;
            }
            return dtype;
        }
    }
}
