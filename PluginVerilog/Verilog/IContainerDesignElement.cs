using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public interface IContainerDesignElement
    {
        /*
            iContainerDesignElement ->  Module
                                        Package
                                        Class
                                        Program
                                        Interface
                                        Root
        */

        /* common item
            net_declaration
            data_declaration
            task_declaration
            function_declaration
            checker_declaration
            local_parameter_declaration
            parameter_declaration
            covergroup_declaration
            overload_declaration
            assertion_item_declaration
            timeunits_declaration
            dpi_import_export
            extern_constraint_declaration
            udp_declaration
        */

        NameSpace Parent { get; }

        //net_declaration
        //data_declaration
        Dictionary<string, Variables.Variable> Variables { get; }

        //task_declaration
        Dictionary<string, Task> Tasks { get; }

        //function_declaration
        Dictionary<string, Function> Functions { get; }

        //checker_declaration
        // TODO

        //local_parameter_declaration
        Dictionary<string, Variables.Parameter> LocalParameters { get; }

        //parameter_declaration
        Dictionary<string, Variables.Parameter> Parameters { get; }

        //covergroup_declaration
        // TODO

        //overload_declaration
        // TODO

        //assertion_item_declaration
        // TODO

        //timeunits_declaration
        // TODO

        //dpi_import_export

        //extern_constraint_declaration

        //udp_declaration

        Dictionary<string, NameSpace> NameSpaces { get; }

        NameSpace GetHierNameSpace(int index);

        Variables.Variable GetVariable(string identifier);

        Variables.Parameter GetParameter(string identifier);

        Dictionary<string, IContainerDesignElement> ConainerDesignElements { get; }
    }
}
