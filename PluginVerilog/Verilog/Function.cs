using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.DataObjects;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects.Nets;
using pluginVerilog.Verilog.DataObjects.Variables;
using pluginVerilog.Verilog.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class Function : NameSpace, IPortNameSpace
    {
        protected Function(NameSpace parent) : base(parent.BuildingBlock, parent)
        {
        }

        private Dictionary<string, DataObjects.Port> ports = new Dictionary<string, DataObjects.Port>();
        public Dictionary<string, DataObjects.Port> Ports { get { return ports; } }
        private List<DataObjects.Port> portsList = new List<DataObjects.Port>();
        public List<DataObjects.Port> PortsList { get { return portsList; } }

        public DataObjects.Variables.Variable ReturnVariable = null;

        public Statements.IStatement Statement;

        private enum valType
        {
            reg,
            integer,
            real,
            realtime,
            time
        }

        public static void Parse(WordScanner word, NameSpace nameSpace)
        {
            if(word.Text != "function")
            {
                System.Diagnostics.Debugger.Break();
            }
            Function function = new Function(nameSpace);
            word.Color(CodeDrawStyle.ColorType.Keyword);
            function.BeginIndexReference = word.CreateIndexReference();
            word.MoveNext();

            // ## verilog 2001
            // function_declaration::= function[automatic][signed][range_or_type] function_identifier;
            // function_item_declaration { function_item_declaration }
            // function_statement
            // endfunction

            // | function[automatic][signed][range_or_type] function_identifier(function_port_list);
            // block_item_declaration { block_item_declaration }
            // function_statement 
            // endfunction 

            // function_item_declaration::= block_item_declaration | tf_input_declaration;
            // function_port_list::= { attribute_instance }
            // tf_input_declaration { , { attribute_instance } tf_input_declaration }
            // range_or_type::= range | integer | real | realtime | time

            // ## SystemVerilog2017
            // function_declaration         ::= "function" [lifetime] function_body_declaration

            // function_body_declaration    ::=   function_data_type_or_implicit [interface_identifier. | class_scope] function_identifier;
            //                                      { tf_item_declaration }
            //                                      { function_statement_or_null }
            //                                      endfunction[ : function_identifier]

            //                                  | function_data_type_or_implicit [interface_identifier. | class_scope] function_identifier([tf_port_list]);
            //                                      { block_item_declaration }
            //                                      { function_statement_or_null }
            //                                      endfunction[ : function_identifier]

            // function_data_type_or_implicit   ::= data_type_or_void | implicit_data_type;
            // signing::= signed | unsigned
            // lifetime::= static | automatic


            // return type  explicit    data_type
            //                          "void"
            //              implicit    (packed dimenstions and,optionally signedness) -> logic scalar
            //                          (empty) -> void

            // [lifetime]
            switch (word.Text)
            {
                case "static":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "automatic":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
            }


            // function_data_type_or_implicit   ::= data_type_or_void | implicit_data_type;
            Variable retVal = null;
            bool returnVoid = false;

            switch (word.Text)
            {
                case "void":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    returnVoid = true;
                    word.MoveNext();
                    break;
                case "signed":
                case "unsigned":
                case "[":
                    {
                        bool signed = false;
                        if(word.Text == "signed")
                        {
                            signed = true;
                            word.Color(CodeDrawStyle.ColorType.Keyword);
                            word.MoveNext();
                        }
                        else if(word.Text == "unsigned")
                        {
                            word.Color(CodeDrawStyle.ColorType.Keyword);
                            word.MoveNext();
                        }
                        List<Range> packedDimensions = new List<Range>();
                        while (word.Text == "[")
                        {
                            Range range = Range.ParseCreate(word, nameSpace);
                            if (range != null) packedDimensions.Add(range);
                        }
                        DataType dataType = Verilog.DataObjects.DataTypes.IntegerVectorType.Create(DataTypeEnum.Logic, signed, packedDimensions);
                        Logic logic = Verilog.DataObjects.Variables.Logic.Create(dataType);
                        logic.PackedDimensions = packedDimensions;
                        retVal = logic;
                    }
                    break;
                default:
                    {
                        DataType dataType = DataType.ParseCreate(word, nameSpace, null);
                        if (dataType != null)
                        {
                            retVal = Verilog.DataObjects.Variables.Variable.Create(dataType);
                        }
                    }
                    break;
            }

            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal identifier name");
                return;
            }

            function.Name = word.Text;
            if (retVal == null & !returnVoid) return;

            if(retVal != null) retVal.Name = function.Name;

            function.ReturnVariable = retVal;

            if (!word.Active)
            {
                // skip
            }
            else
            {
                if(retVal != null && retVal.Name != null)
                {
                    function.DataObjects.Add(retVal.Name, retVal);
                }

                if (nameSpace.BuildingBlock.Functions.ContainsKey(function.Name))
                {
                    nameSpace.BuildingBlock.Functions[function.Name] = function;
                }
                else
                {
                    nameSpace.BuildingBlock.Functions.Add(function.Name, function);
                }
            }

            word.Color(CodeDrawStyle.ColorType.Identifier);
            word.MoveNext();



            /*            
            // function_item_declaration::= block_item_declaration | tf_input_declaration;

            A.2.8 Block item declarations
            
            block_item_declaration ::=
                { attribute_instance } block_reg_declaration |
                { attribute_instance } event_declaration |
                { attribute_instance } integer_declaration |
                { attribute_instance } local_parameter_declaration |
                { attribute_instance } parameter_declaration |
                { attribute_instance } real_declaration |
                { attribute_instance } realtime_declaration |
                { attribute_instance } time_declaration
            
            block_reg_declaration ::= reg[signed][range] list_of_block_variable_identifiers;
            */

            if (word.Text == ";")
            {
                parse_function_items_non_ansi(word, nameSpace, function);
            }
            else
            {
                parse_function_items_ansi(word, nameSpace, function);
            }

            if (word.Text == "endfunction")
            {
                word.AddError("statement required");
            }
            else
            {
                if(word.Prototype)
                {
                    while (!word.Eof)
                    {
                        if (word.Text == "endfunction") break;
                        switch (word.Text)
                        {
                            case "endmodule":
                            case "endtask":
                            case "always":
                            case "initial":
                                word.AddError("missed endfunction");
                                return;
                            default:
                                break;
                        }
                        word.MoveNext();
                    }
                }
                else
                {
                    Statements.IStatement statement = Statements.Statements.ParseCreateFunctionStatement(word, function);
                }
            }

            if(word.Text == "endfunction")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                function.LastIndexReference = word.CreateIndexReference();
                word.AppendBlock(function.BeginIndexReference, function.LastIndexReference);
                word.MoveNext();
            }
            else
            {
                word.AddError("endfunction expected");
                return;
            }

            if(word.Text == ":")
            {
                word.MoveNext();
                if(word.Text == function.Name)
                {
                    word.Color(CodeDrawStyle.ColorType.Identifier);
                    word.MoveNext();
                }
                else
                {
                    word.AddError("function name mismatch");
                    word.MoveNext();
                }
            }



            return;
        }

        // function_body_declaration    ::=   function_data_type_or_implicit [interface_identifier. | class_scope] function_identifier;


        //  ; { tf_item_declaration }
        private static void parse_function_items_non_ansi(WordScanner word, NameSpace nameSpace, Function function)
        {
            if (word.Text != ";") System.Diagnostics.Debugger.Break();
            word.MoveNext();

            while (!word.Eof)
                {
                    switch (word.Text)
                    {
                        case "input": // tf_input_declaration

                            Verilog.DataObjects.Port.ParseTfPortDeclaration(word, function);
                            if (word.Text != ";")
                            {
                                word.AddError("; expected");
                            }
                            else
                            {
                                word.MoveNext();
                            }
                            continue;
                        case "reg": // block_reg_declaration
                            Verilog.DataObjects.Variables.Reg.ParseDeclaration(word, function);
                            continue;
                        // event_declaration
                        case "integer": // integer_declaration
                            Verilog.DataObjects.Variables.Integer.ParseDeclaration(word, function);
                            continue;
                        case "localparameter": // local_parameter_declaration
                        case "paraeter":  // parameter_declaration
                            Verilog.DataObjects.Constants.Parameter.ParseCreateDeclaration(word, function, null);
                            continue;
                        case "real": // real_declaration
                            Verilog.DataObjects.Variables.Real.ParseDeclaration(word, function);
                            continue;
                        case "realtime": // realtime_declaration
                            Verilog.DataObjects.Variables.Realtime.ParseDeclaration(word, function);
                            continue;
                        case "time": // time_declaration
                            Verilog.DataObjects.Variables.Time.ParseDeclaration(word, function);
                            continue;
                        case "wire": // illegal format for Verilog 2001
                            word.AddError("not supported(Veriog2001)");
                            Net.ParseDeclaration(word, function);
                            continue;
                        default:
                            break;
                    }
                    break;
                }
        }

        //  ([tf_port_list]);  { block_item_declaration }

        // tf_port_list ::= tf_port_item { "," tf_port_item } 
        // tf_port_item ::= { attribute_instance } [ tf_port_direction ] [ var ] data_type_or_implicit [ port_identifier { variable_dimension } [ = expression ] ]

        private static void parse_function_items_ansi(WordScanner word, NameSpace nameSpace, Function function)
        {
            if (word.Text != "(") System.Diagnostics.Debugger.Break();
            word.MoveNext();

            Port.ParseTfPortItems(word, nameSpace,function);

            if (word.Text == ")")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError(") required");
            }
            if (word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; required");
            }

            while (!word.Eof)
            {
                if (!BlockItemDeclaration.Parse(word, function)) break;
            }

        }

    }
}
