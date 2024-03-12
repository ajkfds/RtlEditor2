using pluginVerilog.Verilog.BuildingBlocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.ModuleItems
{
    public class ModuleInstantiation : Item,IInstantiation
    {
        protected ModuleInstantiation() { }

        public string SourceName{ get; protected set; }

        public Dictionary<string, Expressions.Expression> ParameterOverrides { get; set; } = new Dictionary<string, Expressions.Expression>();

        public Dictionary<string, Expressions.Expression> PortConnection { get; set; } = new Dictionary<string, Expressions.Expression>();

        public string OverrideParameterID
        {
            get
            {
                if (ParameterOverrides.Count == 0) return "";
                StringBuilder sb = new StringBuilder();
                foreach (var kvp in ParameterOverrides)
                {
                    sb.Append(kvp.Key);
                    sb.Append("=");
                    sb.Append(kvp.Value.Value.ToString());
                    sb.Append(",");
                }
                return sb.ToString();
            }
        }

        public bool Prototype { get; set; } = false;

        public IndexReference BeginIndexReference;
        public IndexReference LastIndexReference;
        public static bool Parse(WordScanner word, NameSpace nameSpace)
        {
            // interface instanciation can be placed only in module
            BuildingBlock buildingBlock = nameSpace.BuildingBlock as BuildingBlock;
            if (buildingBlock == null) return false;

            WordScanner moduleIdentifier = word.Clone();
            string moduleName = word.Text;
            IndexReference beginIndexReference = word.CreateIndexReference();
            Module instancedModule = word.ProjectProperty.GetBuildingBlock(moduleName) as Module;
            if (instancedModule == null)
            {
                return false;
            }
            word.MoveNext();

            string next = word.NextText;
            if(word.Text != "#" && next != "(" && next != ";" && General.IsIdentifier(word.Text))
            {
                moduleIdentifier.AddError("illegal module item");
                word.SkipToKeyword(";");
                return true;
            }
            moduleIdentifier.Color(CodeDrawStyle.ColorType.Keyword);
            ModuleInstantiation moduleInstantiation = new ModuleInstantiation();
            moduleInstantiation.SourceName = moduleName;
            moduleInstantiation.BeginIndexReference = beginIndexReference;
            moduleInstantiation.Project = word.RootParsedDocument.Project;

            


            if (word.Text == "#") // parameter
            {
                word.Color(CodeDrawStyle.ColorType.Keyword);
                word.MoveNext();

                if (word.Text != "(")
                {
                    word.AddError("( expected");
                    word.SkipToKeyword(";");
                    return true;
                }
                word.MoveNext();

                if (word.Text == ".")
                { // named parameter assignment
                    while (!word.Eof && word.Text == ".")
                    {
                        bool error = false;
                        word.MoveNext();
                        word.Color(CodeDrawStyle.ColorType.Paramater);
                        string paramName = word.Text;
                        if (instancedModule != null && !instancedModule.PortParameterNameList.Contains(paramName)){
                            word.AddError("illegal parameter name");
                            error = true;
                        }
                        word.MoveNext();

                        if (word.Text != "(")
                        {
                            word.AddError("( expected");
                        }
                        else
                        {
                            word.MoveNext();
                        }
                        Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
                        if(expression == null)
                        {
                            error = true;
                        }else if (!expression.Constant)
                        {
                            word.AddError("port parameter should be constant");
                            error = true;
                        }

                        if (!error )//& word.Prototype)
                        {
                            if (moduleInstantiation.ParameterOverrides.ContainsKey(paramName))
                            {
                                word.AddPrototypeError("duplicated");
                            }
                            else
                            {
                                moduleInstantiation.ParameterOverrides.Add(paramName, expression);
                            }
                        }

                        if (word.Text != ")")
                        {
                            word.AddError(") expected");
                        }
                        else
                        {
                            word.MoveNext();
                        }
                        if (word.Text != ",")
                        {
                            break;
                        }
                        else
                        {
                            word.MoveNext();
                        }
                    }
                }
                else
                { // ordered paramater assignment
                    int i = 0;
                    while (!word.Eof && word.Text != ")")
                    {
                        Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
                        if(instancedModule != null)
                        {
                            if (i >= instancedModule.PortParameterNameList.Count)
                            {
                                word.AddError("too many parameters");
                            }
                            else
                            {
                                string paramName = instancedModule.PortParameterNameList[i];
                                if (word.Prototype && expression != null)
                                {
                                    if (moduleInstantiation.ParameterOverrides.ContainsKey(paramName))
                                    {
                                        word.AddError("duplicated");
                                    }
                                    else
                                    {
                                        moduleInstantiation.ParameterOverrides.Add(paramName, expression);
                                    }
                                }
                            }

                        }
                        i++;
                        if (word.Text != ",")
                        {
                            break;
                        }
                        else
                        {
                            word.MoveNext();
                        }
                    }
                }

                if (word.Text != ")")
                {
                    word.AddError("( expected");
                    return true;
                }
                word.MoveNext();
            }

            // swap to parameter overrided module
            if( instancedModule != null)
            {
                if (moduleInstantiation.ParameterOverrides.Count != 0)
                {
                    instancedModule = word.ProjectProperty.GetInstancedBuildingBlock(moduleInstantiation) as Module;
                }
                if (instancedModule == null) nameSpace.BuildingBlock.ReperseRequested = true;
            }


            while (!word.Eof)
            {

                word.Color(CodeDrawStyle.ColorType.Identifier);
                if (General.IsIdentifier(word.Text))
                {
                    moduleInstantiation.Name = word.Text;
                }
                else
                {
                    if (word.Prototype) word.AddError("illegal instance name");
                }

                if (word.Prototype)
                {
                    moduleInstantiation.Prototype = true;

                    if (moduleInstantiation.Name == null)
                    {
                        // 
                    }
                    else if (buildingBlock.Instantiations.ContainsKey(moduleInstantiation.Name))
                    {   // duplicated
                        word.AddPrototypeError("instance name duplicated");
                    }
                    else
                    {
                        buildingBlock.Instantiations.Add(moduleInstantiation.Name, moduleInstantiation);
                    }
                }
                else
                {
                    if (moduleInstantiation.Name == null)
                    {
                        // 
                    }
                    else if (buildingBlock.Instantiations.ContainsKey(moduleInstantiation.Name))
                    {   // duplicated
                        if (buildingBlock.Instantiations[moduleInstantiation.Name].Prototype)
                        {
                            moduleInstantiation = buildingBlock.Instantiations[moduleInstantiation.Name] as ModuleInstantiation;
                            moduleInstantiation.Prototype = false;
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        //module.ModuleInstantiations.Add(moduleInstantiation.Name, moduleInstantiation);
                    }
                }

                word.MoveNext();

                if (word.Text != "(")
                {
                    word.AddError("( expected");
                    word.SkipToKeyword(";");
                    if (word.Text == ";") word.MoveNext();
                    return true;
                }
                word.MoveNext();

                if (word.GetCharAt(0) == '.')
                { // named parameter assignment
                    while (!word.Eof && word.Text == ".")
                    {
                        word.MoveNext();
                        string pinName = word.Text;
                        bool outPort = false;
                        word.Color(CodeDrawStyle.ColorType.Identifier);
                        if (instancedModule != null && !word.Prototype) {
                            if (instancedModule.Ports.ContainsKey(pinName))
                            {
                                if(instancedModule.Ports[pinName].Direction == DataObjects.Port.DirectionEnum.Output
                                    || instancedModule.Ports[pinName].Direction == DataObjects.Port.DirectionEnum.Inout )
                                {
                                    outPort = true;
                                }
                            }
                            else
                            {
                                word.AddError("illegal port name");
                            }
                        }
                        if (word.Prototype && moduleInstantiation.PortConnection.ContainsKey(pinName))
                        {
                            word.AddError("duplicated");
                        }
                        word.MoveNext();
                        if (word.Text != "(")
                        {
                            word.AddError("( expected");
                        }
                        else
                        {
                            word.MoveNext();
                        }
                        if (outPort)
                        {
                            Expressions.Expression expression = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);
                            if ( word.Prototype && expression != null && !moduleInstantiation.PortConnection.ContainsKey(pinName)) moduleInstantiation.PortConnection.Add(pinName, expression);

                            if (!word.Prototype)
                            {
                                if (instancedModule != null && expression != null && expression.BitWidth != null && instancedModule.Ports.ContainsKey(pinName))
                                {
                                    if (instancedModule.Ports[pinName].Range == null)
                                    {
                                        if (expression.BitWidth != null && expression.Reference != null && expression.BitWidth != 1)
                                        {
                                            expression.Reference.AddWarning("bitwidth mismatch 1 vs " + expression.BitWidth);
                                        }

                                    }
                                    else if (instancedModule.Ports[pinName].Range.BitWidth != expression.BitWidth && expression.Reference != null)
                                    {
                                        expression.Reference.AddWarning("bitwidth mismatch " + instancedModule.Ports[pinName].Range.BitWidth + " vs " + expression.BitWidth);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
                            if (word.Prototype && expression != null && !moduleInstantiation.PortConnection.ContainsKey(pinName)) moduleInstantiation.PortConnection.Add(pinName, expression);

                            if (!word.Prototype)
                            {
                                if (instancedModule != null && expression != null && expression.BitWidth != null && instancedModule.Ports.ContainsKey(pinName))
                                {
                                    if (instancedModule.Ports[pinName].Range == null)
                                    {
                                        if (expression.BitWidth != null && expression.Reference != null && expression.BitWidth != 1)
                                        {
                                            expression.Reference.AddWarning("bitwidth mismatch 1 vs " + expression.BitWidth);
                                        }

                                    }
                                    else if (instancedModule.Ports[pinName].Range.BitWidth != expression.BitWidth && expression.Reference != null)
                                    {
                                        expression.Reference.AddWarning("bitwidth mismatch " + instancedModule.Ports[pinName].Range.BitWidth + " vs " + expression.BitWidth);
                                    }
                                }
                            }
                        }
                        if (word.Text != ")")
                        {
                            word.AddError(") expected");
                        }
                        else
                        {
                            word.MoveNext();
                        }
                        if (word.Text != ",")
                        {
                            break;
                        }
                        else
                        {
                            word.MoveNext();
                        }
                    }
                }
                else
                { // ordered paramater assignment
                    int i = 0;
                    while (!word.Eof && word.Text != ")")
                    {
                        string pinName = "";
                        if(instancedModule != null && i < instancedModule.PortsList.Count)
                        {
                            pinName = instancedModule.PortsList[i].Name;
                            Expressions.Expression expression = Expressions.Expression.ParseCreate(word,nameSpace);
                            if (word.Prototype && expression != null && !moduleInstantiation.PortConnection.ContainsKey(pinName)) moduleInstantiation.PortConnection.Add(pinName, expression);
                        }
                        else
                        {
                            word.AddError("illegal port connection");
                            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
                        }
                        if (word.Text != ",")
                        {
                            break;
                        }
                        else
                        {
                            word.MoveNext();
                        }
                    }
                }
                if (word.Text != ")")
                {
                    word.AddError(") expected");
                    return true;
                }
                word.MoveNext();
                moduleInstantiation.LastIndexReference = word.CreateIndexReference();

                if (!word.Prototype && word.Active) word.AppendBlock(moduleInstantiation.BeginIndexReference , moduleInstantiation.LastIndexReference);
                if (word.Text != ",") break;
                word.MoveNext();
            }

            if (word.Text != ";")
            {
                word.AddError("; expected");
                return true;
            }
            word.MoveNext();
            return true;
        }

        public BuildingBlock GetInstancedBuildingBlock()
        {
            BuildingBlock instancedModule = ProjectProperty.GetBuildingBlock(SourceName);

            if (ParameterOverrides.Count != 0)
            {
                instancedModule = ProjectProperty.GetInstancedBuildingBlock(this);
            }

            return instancedModule;
        }


        public string CreateString()
        {
            return CreateSrting("\t");

        }
        public string CreateSrting(string indent)
        {
            Module instancedModule = ProjectProperty.GetBuildingBlock(SourceName) as Module;
            if (instancedModule == null) return null;

            StringBuilder sb = new StringBuilder();
            bool first;

            sb.Append(SourceName);
            sb.Append(" ");

            if(instancedModule.PortParameterNameList.Count != 0)
            {
                sb.Append("#(\r\n");

                first = true;
                foreach(var paramName in instancedModule.PortParameterNameList)
                {
                    if (!first) sb.Append(",\r\n");
                    sb.Append(indent);
                    sb.Append(".");
                    sb.Append(paramName);
                    sb.Append("\t( ");
                    if (ParameterOverrides.ContainsKey(paramName))
                    {
                        sb.Append(ParameterOverrides[paramName].CreateString());
                    }
                    else
                    {
                        if(
                            instancedModule.Constants.ContainsKey(paramName) && 
                            instancedModule.Constants[paramName].Expression != null
                            )
                        {
                            sb.Append(instancedModule.Constants[paramName].Expression.CreateString());
                        }
                    }
                    sb.Append(" )");
                    first = false;
                }
                sb.Append("\r\n) ");
            }

            sb.Append(Name);
            sb.Append(" (\r\n");

            first = true;
            string sectionName = null;
            foreach (var port in instancedModule.Ports.Values)
            {
                if (!first) sb.Append(",\r\n");

                if(port.SectionName != sectionName)
                {
                    sectionName = port.SectionName;
                    sb.Append("// ");
                    sb.Append(sectionName);
                    sb.Append("\r\n");
                }
                sb.Append(indent);
                sb.Append(".");
                sb.Append(port.Name);
                sb.Append("\t");
                sb.Append("( ");
                if (PortConnection.ContainsKey(port.Name))
                {
                    sb.Append(PortConnection[port.Name].CreateString());
                }
                sb.Append(" )");
                first = false;
            }
            sb.Append("\r\n);");


            return sb.ToString();
        }

        /*
        module_instantiation            ::= module_identifier [ parameter_value_assignment ] module_instance { , module_instance } ;
        parameter_value_assignment      ::= # ( list_of_parameter_assignments )  
        list_of_parameter_assignments   ::= ordered_parameter_assignment { , ordered_parameter_assignment } | named_parameter_assignment { , named_parameter_assignment }  
        ordered_parameter_assignment    ::= expression  named_parameter_assignment ::= .parameter_identifier ( [ expression ] ) 
        module_instance                 ::= name_of_instance ( [ list_of_port_connections ] )
        name_of_instance                ::= module_instance_identifier [ range ]  
        list_of_port_connections        ::= ordered_port_connection { , ordered_port_connection }          | named_port_connection { , named_port_connection }  
        ordered_port_connection         ::= { attribute_instance } [ expression ]
        named_port_connection           ::= { attribute_instance } .port_identifier ( [ expression ] )  
         */
    }
}
