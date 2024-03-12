using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects;
using System.Data;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    // #SystemVeriog 2017
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


    public class Variable : DataObject, CommentAnnotated
    {
        protected Variable() { }

        /// <summary>
        /// create variable instance from DataType
        /// </summary>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public static Variable Create(DataType dataType)
        {
            /* TODO
                | struct_union["packed"[signing]] { struct_union_member { struct_union_member } } { packed_dimension }
                | "enum" [enum_base_type] { enum_name_declaration { , enum_name_declaration } { packed_dimension }
                | "chandle"
                | "virtual" ["interface"] interface_identifier[parameter_value_assignment][ . modport_identifier] 
                | [class_scope | package_scope] type_identifier { packed_dimension }
                | class_type
                | "event"
                | ps_covergroup_identifier 
                | type_reference
            */
            switch (dataType.Type)
            {
                //integer_vector_type ::= "bit" | "logic" | "reg"
                case DataTypeEnum.Logic:
                case DataTypeEnum.Bit:
                case DataTypeEnum.Reg:
                    return IntegerVectorValueVariable.Create(dataType);
                //integer_atom_type   ::= "byte" | "shortint" | "int" | "longint" | "integer" | "time"
                case DataTypeEnum.Byte:
                case DataTypeEnum.Shortint:
                case DataTypeEnum.Int:
                case DataTypeEnum.Longint:
                case DataTypeEnum.Integer:
                case DataTypeEnum.Time:
                    return IntegerAtomVariable.Create(dataType);

                //non_integer_type    ::= "shortreal" | "real" | "realtime"
                case DataTypeEnum.Shortreal:
                    return Shortreal.Create(dataType);
                case DataTypeEnum.Real:
                    return Real.Create(dataType);
                case DataTypeEnum.Realtime:
                    return Realtime.Create(dataType);

                // "string"
                case DataTypeEnum.String:
                    return String.Create(dataType);
                case DataTypeEnum.Enum:
                    return Enum.Create(dataType);
                default:
                    System.Diagnostics.Debugger.Break();
                    break;
            }
            return null;
        }



        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Name);
        }

        public override void AppendTypeLabel(ajkControls.ColorLabel.ColorLabel label)
        {

        }

        public virtual Variable Clone()
        {
            Variable val = new Variable();
            return val;
        }

        public static bool ParseDeclaration(WordScanner word, NameSpace nameSpace)
        {
            // data_declaration::=    [ const ] [var][lifetime] data_type_or_implicit list_of_variable_decl_assignments;
            //                      | type_declaration
            //                      | package_import_declaration11
            //                      | net_type_declaration

            // list_of_variable_decl_assignments ::= variable_decl_assignment { , variable_decl_assignment } 

            // variable_decl_assignment     ::=   variable_identifier                                   { variable_dimension }  [ = expression]
            //                                  | dynamic_array_variable_identifier unsized_dimension   { variable_dimension }  [ = dynamic_array_new]
            //                                  | class_variable_identifier                                                     [ = class_new]


            DataType dataType = DataObjects.DataTypes.DataType.ParseCreate(word, nameSpace, null);
            if (dataType == null) return false;

            List<Variable> vars = new List<Variable>();

            while (!word.Eof && word.Text !=";")
            {
                string name = word.Text;
                if (!General.IsIdentifier(name))
                {
                    word.AddError("illegal identifier");
                    break;
                }

                Variable variable = Variable.Create(dataType);
                if (variable == null) return true;
                variable.DefinedReference = word.GetReference();

                variable.Name = word.Text;
                switch (dataType.Type)
                {
                    case DataTypeEnum.Reg:
                        word.Color(CodeDrawStyle.ColorType.Register);
                        break;
                    default:
                        word.Color(CodeDrawStyle.ColorType.Variable);
                        break;
                }
                word.MoveNext();

                // { variable_dimension }
                while(word.Text == "[" && !word.Eof)
                {
                    Range range = Range.ParseCreate(word, nameSpace);
                    variable.Dimensions.Add(range);
                }


                if (word.Text == "=") 
                {
                    word.MoveNext();    // =
                    Expressions.Expression exp = Expressions.Expression.ParseCreate(word, nameSpace);
                }

                if (word.Prototype)
                {
                    if (!nameSpace.DataObjects.ContainsKey(variable.Name))
                    {
                        nameSpace.DataObjects.Add(variable.Name, variable);
                    }
                    else
                    {
                        word.AddError("duplicate");
                    }
                }
                else
                {
                    if (!nameSpace.DataObjects.ContainsKey(variable.Name))
                    {
                        nameSpace.DataObjects.Add(variable.Name, variable);
                    }
                }
                vars.Add(variable);

                if (word.Text == ",")
                {
                    word.MoveNext();
                }
                else
                {
                    break;
                }
            }
            if(word.Text == ";")
            {
                word.MoveNext();
            }
            else
            {
                word.AddError("; required");
            }

            string comment = word.GetFollowedComment();

            if(comment != "")
            {
                foreach (Variable variable in vars)
                {
                    variable.Comment = comment;
                }
            }

            return true;
        }


        // comment annotation

        private Dictionary<string, string> commentAnnotations = new Dictionary<string, string>();
        public Dictionary<string, string> CommentAnnotations { get { return commentAnnotations; } }
        public void AppendAnnotation(string key, string value)
        {
            if (commentAnnotations.ContainsKey(key))
            {
                string oldValue = commentAnnotations[key];
                string newValue = oldValue + "," + value;
                commentAnnotations[key] = newValue;
            }
            else
            {
                commentAnnotations.Add(key, value);
            }
        }
        public List<string> GetAnnotations(string key)
        {
            if (commentAnnotations.ContainsKey(key))
            {
                string values = commentAnnotations[key];
                return values.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            }
            else
            {
                return null;
            }
        }

    }

}
