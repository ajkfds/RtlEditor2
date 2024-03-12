using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.DataObjects;
using pluginVerilog.Verilog.DataObjects.DataTypes;
using pluginVerilog.Verilog.DataObjects.Nets;
using pluginVerilog.Verilog.ModuleItems;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pluginVerilog.Verilog.DataObjects
{
    public class Port : Item, CommentAnnotated
    {
        public DirectionEnum Direction = DirectionEnum.Undefined;
        public Range Range
        {
            get
            {
                if (DataObject == null) return null;
                if(DataObject is Net)
                {
                    return (DataObject as Net).Range;
                }
                else
                {
                    if( (DataObject as Variables.Variable) is Variables.IntegerVectorValueVariable)
                    {
                        Variables.IntegerVectorValueVariable vector = DataObject as Variables.IntegerVectorValueVariable;
                        if (vector.PackedDimensions.Count < 1) return null;
                        return vector.PackedDimensions[0];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public DataObject DataObject { set; get; } = null;
        public IInstantiation Instantiation{  set; get; } = null;
        public string Comment = "";
        public string SectionName = "";

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

        public enum DirectionEnum
        {
            Undefined,
            Input,
            Output,
            Inout,
            Ref
        }

        public static Port Create(WordScanner word, Attribute attribute)
        {
            if (!General.IsIdentifier(word.Text)) return null;
            Port port = new Port();
            port.Name = word.Text;
            word.Color(CodeDrawStyle.ColorType.Net);
            word.MoveNext();
            return port;
        }

        public ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();

            switch (Direction)
            {
                case DirectionEnum.Input:
                    label.AppendText("input ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case DirectionEnum.Output:
                    label.AppendText("output ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case DirectionEnum.Inout:
                    label.AppendText("inout ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                case DirectionEnum.Ref:
                    label.AppendText("ref ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
                    break;
                default:
                    break;
            }

            if (DataObject is Variables.Reg)
            {
                label.AppendText("reg ", Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Keyword));
            }


            if (Range != null)
            {
                label.AppendLabel(Range.GetLabel());
                label.AppendText(" ");
            }

            if (DataObject != null)
            {
                if (DataObject is Net)
                {
                    label.AppendText(Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Net));
                }
                else if (DataObject is Variables.Reg)
                {
                    label.AppendText(Name, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Register));
                }
                else
                {
                    label.AppendText(Name);
                }
            }

            return label;
        }

        public static void SyncParser(
            ajkControls.CodeTextbox.Document document,
            ref int nextIndex,
            ref WordPointer.WordTypeEnum wordType,
            ref string sectionName
            )
        {
            int docLength = document.Length;
            char ch;
            for (int j = nextIndex - "@section".Length; j < nextIndex; j++)
            {
                document.SetColorAt(j, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.HighLightedComment));
            }
            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch != ' ' && ch != '\t') break;
                nextIndex++;
            }
            StringBuilder sb = new StringBuilder();
            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch == '\n' || ch == '\r') break;
                document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.HighLightedComment));
                sb.Append(ch);
                nextIndex++;
            }
            sectionName = sb.ToString();

        }



        /*
         * 



        interface_port_declaration ::=    interface_identifier list_of_interface_identifiers 
                                        | interface_identifier . modport_identifier list_of_interface_identifiers 
        ref_declaration ::=       ref variable_port_type list_of_variable_identifiers 

parameter_port_list ::= 
# ( list_of_param_assignments { , parameter_port_declaration } )
| # ( parameter_port_declaration { , parameter_port_declaration } )
| #( )
parameter_port_declaration ::= 
parameter_declaration 
| local_parameter_declaration 
| data_type list_of_param_assignments 
| type list_of_type_assignments 
list_of_ports ::= ( port { , port } )
list_of_port_declarations2 ::= 
( [ { attribute_instance} ansi_port_declaration { , { attribute_instance} ansi_port_declaration } ] 

port_declaration ::= 
{ attribute_instance } inout_declaration 
| { attribute_instance } input_declaration 
| { attribute_instance } output_declaration 
| { attribute_instance } ref_declaration 
| { attribute_instance } interface_port_declaration 
port ::= 
[ port_expression ] 
| . port_identifier ( [ port_expression ] )
port_expression ::= 
port_reference 
| { port_reference { , port_reference } }
port_reference ::= 
port_identifier constant_select 
port_direction ::= input | output | inout | ref
net_port_header ::= [ port_direction ] net_port_type 
variable_port_header ::= [ port_direction ] variable_port_type 
interface_port_header ::= 
interface_identifier [ . modport_identifier ] 
| interface [ . modport_identifier ] 
ansi_port_declaration ::= 
[ net_port_header | interface_port_header ] port_identifier { unpacked_dimension } 
[ = constant_expression ] 
| [ variable_port_header ] port_identifier { variable_dimension } [ = constant_expression ] 
| [ port_direction ] . port_identifier ( [ expression ] )         
         */


        // ## verilog 2001
        // port_declaration::= { attribute_instance} inout_declaration | { attribute_instance} input_declaration | { attribute_instance} output_declaration  

        // inout_declaration::= inout[net_type][signed][range] list_of_port_identifiers
        // input_declaration ::= input[net_type][signed][range] list_of_port_identifiers
        // output_declaration ::=   output[net_type][signed][range] list_of_port_identifiers
        //                          | output[reg][signed][range] list_of_port_identifiers
        //                          | output reg[signed][range] list_of_variable_port_identifiers
        //                          | output[output_variable_type] list_of_port_identifiers
        //                          | output output_variable_type list_of_variable_port_identifiers 
        // list_of_port_identifiers::= port_identifier { , port_identifier }
        // range ::= [ msb_constant_expression : lsb_constant_expression ]  

        public static void ParsePortDeclarations(WordScanner word,NameSpace nameSpace)
        {
            DataType prevDataType = null;
            Net.NetTypeEnum? prevNetType = null;
            DirectionEnum? prevDirection = null;

            if (ParsePortDeclaration(word, nameSpace, true, ref prevDataType, ref prevNetType, ref prevDirection))
            {
                if (word.Text != ",") return;
                word.MoveNext();
                while (!word.Eof)
                {
                    if (!ParsePortDeclaration(word, nameSpace, false, ref prevDataType, ref prevNetType, ref prevDirection)) return;
                    if (word.Text != ",") return;
                    word.MoveNext();
                }
            }
            else if(!nameSpace.BuildingBlock.AnsiStylePortDefinition)
            {
                while (!word.Eof)
                {
                    if (!ParseNonAnsiPortDeclaration(word, nameSpace)) return;
                    if (word.Text != ",") return;
                    word.MoveNext();
                }
            }
        }

        private static bool ParseNonAnsiPortDeclaration(WordScanner word, NameSpace nameSpace)
        {
            if (!General.IsIdentifier(word.Text)) return false;

            Port port = new Port();
            port.Name = word.Text;
            port.Direction = DirectionEnum.Undefined;
            IModuleOrInterfaceOrProgram block = nameSpace.BuildingBlock as IModuleOrInterfaceOrProgram;
            if (block.Ports.ContainsKey(port.Name))
            {
                block.Ports.Add(port.Name, port);
            }
            word.Color(CodeDrawStyle.ColorType.Variable);
            word.MoveNext();

            return true;
        }

        private static bool ParsePortDeclaration(WordScanner word, NameSpace nameSpace, bool firstPort, ref DataType prevDataType, ref Net.NetTypeEnum? prevNetType, ref DirectionEnum? prevDirection)
        {


            // # SystemVerilog 2012
            // A.2.1.2 Port declarations

            // Non-ANSI style port declaration

            // port_declaration     ::=   { attribute_instance } inout_declaration
            //                          | { attribute_instance } input_declaration
            //                          | { attribute_instance } output_declaration
            //                          | { attribute_instance } ref_declaration
            //                          | { attribute_instance } interface_port_declaration

            // inout_declaration ::=     "inout" net_port_type list_of_port_identifiers 
            // input_declaration ::=     "input" net_port_type list_of_port_identifiers 
            //                         | "input" variable_port_type list_of_variable_identifiers 
            // output_declaration ::=    "output" net_port_type list_of_port_identifiers 
            //                         | "output" variable_port_type list_of_variable_port_identifiers
            // ref_declaration ::= ref variable_port_type list_of_variable_identifiers

            // interface_port_declaration   ::=   interface_identifier list_of_interface_identifiers 
            //                                  | interface_identifier . modport_identifier list_of_interface_identifiers 

            // net_port_type				::=   [ net_type ] data_type_or_implicit 
            //								    | net_type_identifier 
            //								    | interconnect implicit_data_type
            // variable_port_type			::= var_data_type 
            // var_data_type				::=   data_type
            //								    | var data_type_or_implicit 

            // ANSI style list of port declarations

            // list_of_port_declarations	::=	( [ { attribute_instance} ansi_port_declaration { , { attribute_instance} ansi_port_declaration } ] )

            // ansi_port_declaration		::=   [ net_port_header | interface_port_header ] port_identifier { unpacked_dimension } [ = constant_expression ] 
            //								    | [ variable_port_header ]                    port_identifier { variable_dimension } [ = constant_expression ] 
            //								    | [ port_direction ] "." port_identifier "(" [ expression ] ")"

            // net_port_header				::= [ port_direction ] net_port_type 
            // variable_port_header			::= [ port_direction ] variable_port_type 
            // interface_port_header		::=   interface_identifier [ "." modport_identifier ] 
            //								    | "interface" [ "." modport_identifier ] 

            // port_direction				::= "input" | "output" | "inout" | "ref"

            // net_port_type				::=   [ net_type ] data_type_or_implicit 
            //								    | net_type_identifier 
            //								    | interconnect implicit_data_type
            // variable_port_type			::= var_data_type 
            // var_data_type				::=   data_type
            //								    | var data_type_or_implicit 


            /*
            23.2.2.3 Rules for determining port kind, data type, and direction
            
            in this subclause :

            port kinds = net_type keywords or "var"
            data type  = explicit and implicit data type declarations
                         and does not include unpacked dimensions.
             */

            BuildingBlock buildingBlock = nameSpace.BuildingBlock;
            DirectionEnum? direction = null;
            switch (word.Text)
            {
                case "input":
                    direction = DirectionEnum.Input;
                    break;
                case "output":
                    direction = DirectionEnum.Output;
                    break;
                case "inout":
                    direction = DirectionEnum.Inout;
                    break;
                default:
                    break;
            }
            if(direction != null)
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }

            Net.NetTypeEnum? netType = Net.parseNetType(word, nameSpace);

            DataType dataType = null;
            if(netType == null)
            {
                dataType = DataObjects.DataTypes.DataType.ParseCreate(word, nameSpace, null);
            }

            Interface interface_ = null;
            if(direction == null && netType == null && dataType == null)
            {
                if (parseInterfacePort(word, nameSpace))
                {
                    prevDirection = null;
                    prevDataType = null;
                    prevNetType = null;
                    return true;
                }
            }

            if ( direction == null && netType == null && dataType == null && interface_ == null )
            {
                if (firstPort)
                {
                    //For the first port in the port list:
                    //  — If the direction, port kind, and data type are all omitted, then the port shall be assumed to be a
                    //    member of a non - ANSI style list_of_ports, and port direction and type declarations shall be declared
                    //    after the port list. 
                    nameSpace.BuildingBlock.AnsiStylePortDefinition = false;
                    return false;
                }
                else
                {
                    //For subsequent ports in the port list:
                    //    — If the direction, port kind and data type are all omitted, then they shall be inherited from the previous port.
                    //      If the previous port was an interconnect port, this port shall also be an interconnect port.
                    direction = prevDirection;
                    dataType = prevDataType;
                    netType = prevNetType;
                }
            }

            //    — If the direction is omitted, it shall default to inout. 
            if (direction == null) direction = DirectionEnum.Inout;

            //If the port kind is omitted: (port kinds = net_type keywords or "var")
            if (netType == null)
            {
                switch (direction)
                {
                    //    — For input and inout ports, the port shall default to a net of default net type. 
                    case DirectionEnum.Inout:
                    case DirectionEnum.Input:
                        netType = buildingBlock.DefaultNetType;
                        break;
                    //    — For output ports, the default port kind depends on how the data type is specified:
                    //      — If the data type is omitted or declared with the implicit_data_type syntax, the port kind shall
                    //        default to a net of default net type.
                    //      — If the data type is declared with the explicit data_type syntax, the port kind shall default to variable.
                    //      — A ref port is always a variable.
                    case DirectionEnum.Output:
                        if(dataType == null)
                        {
                            netType = buildingBlock.DefaultNetType;
                        }
                        break;
                }
            }

            //    — If the data type is omitted, it shall default to logic except for interconnect ports which have no data type.
            //if (direction != DirectionEnum.Inout && netType == null)
            //{
            //    dataType = DataType.ParseCreate(word, nameSpace, null);
            //}

            // parse packed dimensions for net without explicit datatype
            List<Range> packedDimensions = new List<Range>();
            if(dataType == null)
            {
                while (word.Text=="[")
                {
                    Range range = Range.ParseCreate(word, nameSpace);
                    if (range == null) break;
                    packedDimensions.Add(range);
                }
            }

            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal port identifier");
                return true;
            }


            Port port = new Port();
            port.Name = word.Text;
            port.Direction = (DirectionEnum)direction;
            if(netType != null)
            {
                Net net = Net.Create((Net.NetTypeEnum)netType, dataType);
                net.PackedDimensions = packedDimensions;
                net.Name = port.Name;
                port.DataObject = net;
            }
            else if(dataType != null)
            {
                Variables.Variable variable = Variables.Variable.Create(dataType);
                variable.Name = port.Name;
                port.DataObject = variable;
            }
            else if(interface_ != null)
            {
//                Interface interface_instance = interface_ .
            }

            addPort(word, nameSpace, port);

            word.Color(CodeDrawStyle.ColorType.Variable);
            word.MoveNext();


            // Unpacked dimensions shall not be inherited from the previous port declaration
            //  and must be repeated for each port with the same dimensions.
            // { dimension } 
            while (!word.Eof && word.Text == "[")
            {
                Range dimension = Range.ParseCreate(word, nameSpace);
                if(dimension != null)
                {
                    port.DataObject.Dimensions.Add(dimension);
                }
            }

            // [ = constant_expression ] 
            if (word.Text == "=")
            {
                word.MoveNext();
                Expressions.Expression ex = Expressions.Expression.ParseCreate(word, nameSpace);
                if (!ex.Constant)
                {
                    ex.Reference.AddError("should be constant");
                }
                else
                {
                    // TODO contant value assignment
                }
            }

            prevDirection = direction;
            prevDataType = dataType;
            prevNetType = netType;

            return true;
        }

        private static void addInstantiation(WordScanner word, NameSpace nameSpace, IInstantiation instantiation)
        {
            BuildingBlock block = nameSpace.BuildingBlock as BuildingBlock;
            if (block == null)
            {
                word.AddError("cannot add instantiation");
            }
            else
            {
                if (block.Instantiations.ContainsKey(instantiation.Name))
                {
                    if (word.Prototype)
                    {
                        word.AddError("port name duplicate");
                    }
                    else
                    {
                        block.Instantiations.Remove(instantiation.Name);
                        block.Instantiations.Add(instantiation.Name, instantiation);
                    }
                }
                else
                {
                    block.Instantiations.Add(instantiation.Name, instantiation);
                }
            }
        }

        private static void addPort(WordScanner word, NameSpace nameSpace,Port port)
        {
            IModuleOrInterfaceOrProgram block = nameSpace.BuildingBlock as IModuleOrInterfaceOrProgram;
            if (block == null)
            {
                word.AddError("cannot add port");
            }
            else
            {
                if (block.Ports.ContainsKey(port.Name))
                {
                    if (word.Prototype)
                    {
                        word.AddError("port name duplicate");
                    }
                    else
                    {
                        block.Ports.Remove(port.Name);
                        block.Ports.Add(port.Name, port);
                        block.PortsList.Add(port);
                    }
                }
                else
                {
                    block.Ports.Add(port.Name, port);
                    block.PortsList.Add(port);
                }

                if (port.DataObject != null)
                {
                    port.DataObject.Name = port.Name;
                    if (block.DataObjects.ContainsKey(port.Name))
                    {
                        if (word.Prototype)
                        {
                            //                    word.AddError("port name duplicate");
                        }
                        else
                        {
                            block.DataObjects.Remove(port.Name);
                            block.DataObjects.Add(port.Name, port.DataObject);
                        }
                    }
                    else
                    {
                        block.DataObjects.Add(port.Name, port.DataObject);
                    }
                }
            }

        }

        private static bool parseInterfacePort(WordScanner word, NameSpace nameSpace)
        {
            Interface interface_ = null;
            string identifier = word.Text;
            interface_ = word.ProjectProperty.GetBuildingBlock(identifier) as Interface;
            if (interface_ == null) return false;

            BuildingBlock buildingBlock = nameSpace.BuildingBlock;
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();


            string instance_name = word.Text;
            if (!General.IsIdentifier(instance_name))
            {
                word.AddError("illegal identifier");
                return true;
            }


            InterfaceInstantiation iinst = InterfaceInstantiation.Create(instance_name, interface_.Name, word.Project);
            word.Color(CodeDrawStyle.ColorType.Identifier);
            word.MoveNext();

            Port port = new Port();
            port.Name = instance_name;
            port.Instantiation = iinst;

            addPort(word, nameSpace, port);
            addInstantiation(word, nameSpace, iinst);

            return true;
        }


        // tf_port_item         ::= { attribute_instance } [tf_port_direction] [var] data_type_or_implicit [port_identifier { variable_dimension } [ = expression] ] 

        // tf_port_direction    ::= port_direction | "const ref"
        // tf_port_declaration  ::= { attribute_instance } tf_port_direction [var] data_type_or_implicit list_of_tf_variable_identifiers;
        // task_prototype       ::= task task_identifier[( [tf_port_list])]


        // ### task port

        /*
        tf_item_declaration ::=   block_item_declaration 
                                | tf_port_declaration 

        tf_port_list ::=        tf_port_item { , tf_port_item }

        tf_port_item ::=    { attribute_instance } [ tf_port_direction ] [ var ] data_type_or_implicit [ port_identifier { variable_dimension } [ = expression ] ] 
        tf_port_direction ::=   port_direction | "const ref"

        tf_port_declaration ::= 
            { attribute_instance } tf_port_direction [ "var" ] data_type_or_implicit list_of_tf_variable_identifiers ";"


        // task / function port

        // tf_port_list / tf_port_item style ------------------------------
        
        tf_port_list::= tf_port_item { , tf_port_item }
        tf_port_item         ::= { attribute_instance } [ tf_port_direction ] [ var ] data_type_or_implicit [ port_identifier { variable_dimension } [ = expression ] ]


        // tf_item_declaration / tf_port_declaration style ----------------

        tf_item_declaration     ::=   block_item_declaration 
                                    | tf_port_declaration 
        tf_port_declaration     ::= { attribute_instance } tf_port_direction [ var ] data_type_or_implicit list_of_tf_variable_identifiers ;

        // common
        tf_port_direction::= port_direction | "const" "ref"

        data_type_or_implicit   ::=   data_type 
                                    | implicit_data_type 
        implicit_data_type ::=        [ signing ] { packed_dimension }          



        lifetime                ::= "static" | "automatic"
        signing                 ::= "signed" | "unsigned"
        */

        public static bool ParseTfPortDeclaration(WordScanner word, NameSpace nameSpace)
        {
            // tf_port_direction[var] data_type_or_implicit list_of_tf_variable_identifiers;
            BuildingBlock buildingBlock = nameSpace.BuildingBlock;
            DirectionEnum? direction = null;
            switch (word.Text)
            {
                case "input":
                    direction = DirectionEnum.Input;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "output":
                    direction = DirectionEnum.Output;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "inout":
                    direction = DirectionEnum.Inout;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "const":
                    direction = DirectionEnum.Ref;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    if (word.Text == "ref")
                    {
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                    }
                    else
                    {
                        word.AddError("ref required");
                    }
                    break;
                default:
                    return false;
            }

            if (word.Text == "var")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }

            DataType dataType = DataType.ParseCreate(word, nameSpace, null);

            // Each formal argument has a data type that can be explicitly declared or inherited from the previous argument.
            // If the data type is not explicitly declared, then the default data type is logic
            //    if it is the first argument
            // or if the argument direction is explicitly specified.
            // Otherwise, the data type is inherited from the previous argument.

            if (dataType == null)
            {
                    DataTypes.IntegerVectorType vectorType = new DataTypes.IntegerVectorType();
                    vectorType.Type = DataTypeEnum.Logic;
                    if(word.Text == "[")
                    {
                    Range range = Range.ParseCreate(word, nameSpace);
                    vectorType.PackedDimensions.Add(range);

                    }

                dataType = vectorType;
            }

            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal port name");
                return true;
            }

            while (!word.Eof)
            {
                if (!General.IsIdentifier(word.Text)) break;

                Port port = new Port();
                port.Name = word.Text;
                port.Direction = (DirectionEnum)direction;
                port.DataObject = Variables.Variable.Create(dataType);
                port.DataObject.Name = port.Name;

                if( nameSpace is Function)
                {
                    Function function = nameSpace as Function;
                    if (!function.Ports.ContainsKey(port.Name))
                    {
                        function.Ports.Add(port.Name, port);
                        function.PortsList.Add(port);
                    }
                }else if(nameSpace is Task)
                {
                    Task task = nameSpace as Task;
                    if (!task.Ports.ContainsKey(port.Name))
                    {
                        task.Ports.Add(port.Name, port);
                        task.PortsList.Add(port);
                    }
                }

                if (!nameSpace.DataObjects.ContainsKey(port.DataObject.Name))
                {
                    nameSpace.DataObjects.Add(port.DataObject.Name, port.DataObject);
                }

                word.Color(CodeDrawStyle.ColorType.Variable);
                word.MoveNext();

                if (word.Text != ",") return true;
                word.MoveNext();
            }
            return true;
        }

        public static void ParseTfPortItems(WordScanner word, NameSpace nameSpace, IPortNameSpace portNameSpace)
        {
            DirectionEnum? prevDirection = null;
            DataType prevDataType = null;

            bool firstPort = true;
            portNameSpace.Ports.Clear();
            portNameSpace.PortsList.Clear();


            while (!word.Eof && word.Text != ")" && word.Text != "end")
            {
                ParseTfPortItem(word, nameSpace, portNameSpace, firstPort, ref prevDirection, ref prevDataType);
                if(word.Text == ",")
                {
                    word.MoveNext();
                }
                else
                {
                    return;
                }
            }

        }

        public static bool ParseTfPortItem(WordScanner word, NameSpace nameSpace, IPortNameSpace portNameSpace, bool first,ref DirectionEnum? prevDirection, ref DataType prevDataType)
        {
            // tf_port_item    ::= { attribute_instance } [ tf_port_direction ] [ var ] data_type_or_implicit [ port_identifier { variable_dimension } [ = expression ] ]

            BuildingBlock buildingBlock = nameSpace.BuildingBlock;
            DirectionEnum? direction = null;
            switch (word.Text)
            {
                case "input":
                    direction = DirectionEnum.Input;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "output":
                    direction = DirectionEnum.Output;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "inout":
                    direction = DirectionEnum.Inout;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    break;
                case "const":
                    direction = DirectionEnum.Ref;
                    word.Color(CodeDrawStyle.ColorType.Keyword);
                    word.MoveNext();
                    if(word.Text == "ref")
                    {
                        word.Color(CodeDrawStyle.ColorType.Keyword);
                        word.MoveNext();
                    }
                    else
                    {
                        word.AddError("ref required");
                    }
                    break;
                default:
                    break;
            }

            if(word.Text == "var")
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();
            }

            DataType dataType = DataType.ParseCreate(word, nameSpace, null);

            // Each formal argument has a data type that can be explicitly declared or inherited from the previous argument.
            // If the data type is not explicitly declared, then the default data type is logic
            //    if it is the first argument
            // or if the argument direction is explicitly specified.
            // Otherwise, the data type is inherited from the previous argument.

            if(dataType == null)
            {
                if(first || direction != null)
                {
                    DataTypes.IntegerVectorType vectorType = new DataTypes.IntegerVectorType();
                    vectorType.Type = DataTypeEnum.Logic;
                    if (word.Text == "[")
                    {
                        Range range = Range.ParseCreate(word, nameSpace);
                        vectorType.PackedDimensions.Add(range);
                    }

                    dataType = vectorType;
                }
                else
                {
                    dataType = prevDataType;
                }
            }

            // There is a default direction of input if no direction has been specified. Once a direction is given,
            // subsequent formals default to the same direction. 
            if(direction == null)
            {
                direction = DirectionEnum.Input;
            }

            if (!General.IsIdentifier(word.Text))
            {
                word.AddError("illegal port name");
                word.SkipToKeyword(",");
            }

            Port port = new Port();
            port.Direction = (DirectionEnum)direction;
            port.DataObject = Variables.Variable.Create(dataType);
            port.Name = word.Text;
            port.DataObject.Name = port.Name;
            word.Color(CodeDrawStyle.ColorType.Variable);

            if (portNameSpace.Ports.ContainsKey(port.Name))
            {
                if (portNameSpace.Ports.ContainsKey(port.Name))
                {
                    word.AddError("port name duplicated");
                }
            }
            else
            {
                portNameSpace.Ports.Add(port.Name, port);
                portNameSpace.PortsList.Add(port);
            }

            if (portNameSpace.DataObjects.ContainsKey(port.DataObject.Name))
            {
                if (word.Prototype)
                {
                }
                else
                {
                    if (portNameSpace.DataObjects.ContainsKey(port.DataObject.Name)) portNameSpace.DataObjects.Remove(port.DataObject.Name);
                }
                portNameSpace.DataObjects.Add(port.DataObject.Name, port.DataObject);
            }
            else
            {
                portNameSpace.DataObjects.Add(port.DataObject.Name, port.DataObject);
            }


            word.MoveNext();

            prevDirection = port.Direction;
            prevDataType = dataType;
            return true;
        }



    }
}