using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.DataObjects;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Constants
{

    // parameter, localparam, and specparam.
    public class Constants : DataObject
    {
        public Expressions.Expression Expression;
        public WordReference DefinitionRefrecnce { get; set; }

        public enum ConstantTypeEnum
        {
            parameter,
            localparam,
            specparam,
            enum_
        }
        public ConstantTypeEnum ConstantType = ConstantTypeEnum.parameter;

        public static void ParseCreateDeclarationForPort(WordScanner word, IModuleOrInterfaceOrProgram module, Attribute attribute)
        {
            /*
            local_parameter_declaration ::=  (From Annex A - A.2.1.1)  
                                            localparam [ signed ] [ range ] list_of_param_assignments ; 
                                            | localparam integer list_of_param_assignments ; 
                                            | localparam real list_of_param_assignments ; 
                                            | localparam realtime list_of_param_assignments ; 
                                            | localparam time list_of_param_assignments ; 
            parameter_declaration       ::=  parameter [ signed ] [ range ] list_of_param_assignments ;
                                            | parameter integer list_of_param_assignments ; 
                                            | parameter real list_of_param_assignments ; 
                                            | parameter realtime list_of_param_assignments ; 
                                            | parameter time list_of_param_assignments ;
            list_of_param_assignments ::= (From Annex A - A.2.3) param_assignment { , param_assignment }  
            param_assignment ::= (From Annex A - A.2.4) parameter_identifier = constant_expression  
            range ::=  (From Annex A - A.2.5) [ msb_constant_expression : lsb_constant_expression ]              
            */

            /* ## SystemVerilog
             * 
            local_parameter_declaration ::=
                  "localparam" data_type_or_implicit list_of_param_assignments 
                | "localparam" type list_of_type_assignments 
            parameter_declaration ::= 
                  "parameter" data_type_or_implicit list_of_param_assignments 
                | "parameter" type list_of_type_assignments 
            specparam_declaration ::= 
                  "specparam" [ packed_dimension ] list_of_specparam_assignments ;
             */
            ConstantTypeEnum constantType = ConstantTypeEnum.parameter;
            if (word.Text == "parameter")
            {
                constantType = ConstantTypeEnum.parameter;
            }
            else if (word.Text == "localparam")
            {
                constantType = ConstantTypeEnum.localparam;
            }
            else if(word.Text == "specparam")
            {
                constantType = ConstantTypeEnum.specparam;
            }
            else
            {
                System.Diagnostics.Debugger.Break();
            }
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            DataObjects.DataTypes.DataType dataType = DataObjects.DataTypes.DataType.ParseCreate(word, (NameSpace)module, null);

            switch (word.Text)
            {
                case "integer":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "real":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "realtime":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "time":
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                default:
                    if (word.Text == "signed")
                    {
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                    }
                    if (word.GetCharAt(0) == '[')
                    {
                        Range range = Range.ParseCreate(word, (NameSpace)module);
                    }
                    break;
            }

            WordReference nameReference;
            while (!word.Eof)
            {
                if (!General.IsIdentifier(word.Text)) break;
                string identifier = word.Text;
                nameReference = word.GetReference();
                word.Color(CodeDrawStyle.ColorType.Paramater);
                word.MoveNext();

                if (word.Text != "=") break;
                word.MoveNext();
                Expressions.Expression expression = Expressions.Expression.ParseCreate(word, (NameSpace)module);
                if (expression == null) break;
                if (word.Active)
                {
                    //if (local)
                    //{
                    //    if (!word.Active)
                    //    {

                    //    }
                    //    else if (word.Prototype)
                    //    {
                    //        if (module.LocalParameters.ContainsKey(identifier))
                    //        {
                    //            //                                nameReference.AddError("local parameter name duplicated");
                    //        }
                    //        else
                    //        {
                    //            Parameter param = new Parameter();
                    //            param.Name = identifier;
                    //            param.Expression = expression;
                    //            param.DefinitionRefrecnce = nameReference;
                    //            module.LocalParameters.Add(param.Name, param);
                    //        }
                    //    }
                    //    else
                    //    {

                    //    }
                    //}
                    //else
                    {
                        if (!word.Active)
                        {
                            // skip
                        }
                        else if (word.Prototype)
                        {
                            if (module.Constants.ContainsKey(identifier))
                            {
                                //                                nameReference.AddError("parameter name duplicated");
                            }
                            else
                            {
                                Constants constants;
                                switch (constantType)
                                {
                                    case ConstantTypeEnum.localparam:
                                        constants = new Localparam();
                                        break;
                                    case ConstantTypeEnum.parameter:
                                        constants = new Parameter();
                                        break;
                                    case ConstantTypeEnum.specparam:
                                        constants = new Specparam();
                                        break;
                                    default:
                                        System.Diagnostics.Debugger.Break();
                                        return;
                                }
                                constants.Name = identifier;
                                constants.Expression = expression;
                                constants.DefinitionRefrecnce = nameReference;
                                constants.ConstantType = constantType;
                                module.Constants.Add(constants.Name, constants);

                                module.PortParameterNameList.Add(identifier);
                            }
                        }
                        else
                        {

                        }
                    }
                }
                if (word.Text != ",") break;
                if (word.NextText == "parameter") break;
                word.MoveNext();
            }
        }
        public static void ParseCreateDeclaration(WordScanner word, NameSpace nameSpace, Attribute attribute)
        {
            /*
            local_parameter_declaration ::=  (From Annex A - A.2.1.1)  
                                            localparam [ signed ] [ range ] list_of_param_assignments ; 
                                            | localparam integer list_of_param_assignments ; 
                                            | localparam real list_of_param_assignments ; 
                                            | localparam realtime list_of_param_assignments ; 
                                            | localparam time list_of_param_assignments ; 
            parameter_declaration       ::=  parameter [ signed ] [ range ] list_of_param_assignments ;
                                            | parameter integer list_of_param_assignments ; 
                                            | parameter real list_of_param_assignments ; 
                                            | parameter realtime list_of_param_assignments ; 
                                            | parameter time list_of_param_assignments ;
            list_of_param_assignments ::= (From Annex A - A.2.3) param_assignment { , param_assignment }  
            param_assignment ::= (From Annex A - A.2.4) parameter_identifier = constant_expression  
            range ::=  (From Annex A - A.2.5) [ msb_constant_expression : lsb_constant_expression ]              
            */

            /* ## SystemVerilog
            local_parameter_declaration     ::=   "localparam" data_type_or_implicit list_of_param_assignments 
                                                | "localparam" type list_of_type_assignments 
            parameter_declaration           ::=   "parameter" data_type_or_implicit list_of_param_assignments 
                                                | "parameter" type list_of_type_assignments 

            specparam_declaration           ::=   "specparam" [ packed_dimension ] list_of_specparam_assignments ";"

            data_type_or_implicit           ::=   data_type
                                                | implicit_data_type
            implicit_data_type              ::=   [ signing ] { packed_dimension } 

            list_of_param_assignments       ::= param_assignment { , param_assignment }
            list_of_specparam_assignments   ::= specparam_assignment { , specparam_assignment }
            list_of_type_assignments        ::= type_assignment { , type_assignment } 

            param_assignment                ::= parameter_identifier { unpacked_dimension } [ = constant_param_expression ]
            
            specparam_assignment            ::= specparam_identifier = constant_mintypmax_expression 
                                                | pulse_control_specparam 

            type_assignment                 ::= type_identifier [ = data_type ]
             */

            ConstantTypeEnum constantType = ConstantTypeEnum.parameter;
            if (word.Text == "parameter")
            {
                constantType = ConstantTypeEnum.parameter;
            }
            else if (word.Text == "localparam")
            {
                constantType = ConstantTypeEnum.localparam;
            }
            else if (word.Text == "specparam")
            {
                constantType = ConstantTypeEnum.specparam;
            }
            else
            {
                System.Diagnostics.Debugger.Break();
            }

            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            DataType dataType = DataObjects.DataTypes.DataType.ParseCreate(word, nameSpace, null);
            Range range = null;
            bool signed = false;

            if (word.Text == "signed")
            {
                signed = true;
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }

            if (word.GetCharAt(0) == '[')
            {
                range = Range.ParseCreate(word, nameSpace);
            }

            while (!word.Eof)
            {
                if (!General.IsIdentifier(word.Text)) break;
                string identifier = word.Text;
                word.Color(CodeDrawStyle.ColorType.Paramater);
                Constants constants;
                switch (constantType)
                {
                    case ConstantTypeEnum.localparam:
                        constants = new Localparam();
                        break;
                    case ConstantTypeEnum.parameter:
                        constants = new Parameter();
                        break;
                    case ConstantTypeEnum.specparam:
                        constants = new Specparam();
                        break;
                    default:
                        System.Diagnostics.Debugger.Break();
                        return;
                }

                {
                    if (!word.Active)
                    {
                        // skip
                    }
                    else if (word.Prototype)
                    {
                        if (nameSpace.Constants.ContainsKey(identifier))
                        {
                            word.AddError("name duplicated");
                        }
                        else
                        {
                            constants.Name = identifier;
                            nameSpace.Constants.Add(constants.Name, constants);
                        }
                    }
                    else
                    {
                        if (nameSpace.Constants.ContainsKey(identifier))
                        { // re-parse after prototype parse 
                            constants = nameSpace.Constants[identifier];
                        }
                        else
                        { // for root nameSpace parameter
                            constants.Name = identifier;
                            nameSpace.Constants.Add(constants.Name, constants);
                        }
                    }
                }
                word.MoveNext();

                if (word.Text != "=") break;
                word.MoveNext();

                constants.Expression = Expressions.Expression.ParseCreate(word, nameSpace);
                if (constants.Expression == null) break;

                if (word.Text != ",") break;
                word.MoveNext();
            }

            if (word.GetCharAt(0) == ';')
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; expected");
                word.SkipToKeyword(";");
                if (word.Text == ";") word.MoveNext();
            }
        }
    }
}
