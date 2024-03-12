using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Statements
{
    public class TaskEnable : IStatement
    {
        // task_enable ::= (From Annex A - A.6.9) hierarchical_task_identifier [ ( expression { , expression } ) ] ;
        public void DisposeSubReference()
        {
        }
        public static TaskEnable ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            return ParseCreate(word, nameSpace, nameSpace);
        }
        public static TaskEnable ParseCreate(WordScanner word, NameSpace nameSpace,NameSpace taskNameSpace)
        {
            Expressions.TaskReference taskReference = Verilog.Expressions.TaskReference.ParseCreate(word, nameSpace, taskNameSpace);
            return ParseCreate(taskReference, word, nameSpace);
        }

        public static TaskEnable ParseCreate(Expressions.TaskReference taskReference,WordScanner word,NameSpace nameSpace)
        {

            IPortNameSpace task = taskReference.Task;
            return parseCreate(task, word, nameSpace);
        }

        private static TaskEnable parseCreate(IPortNameSpace task, WordScanner word, NameSpace nameSpace)
        {
            TaskEnable taskEnable = new TaskEnable();
            int portCount = 0;

            if (word.Text == "(")
            {
                word.MoveNext();
                while (!word.Eof)
                {
                    Expressions.Expression expression;
                    if (task == null)
                    {
                        expression = Expressions.Expression.ParseCreate(word, nameSpace);
                    }
                    else if (portCount == task.PortsList.Count)
                    {
                        word.AddError("too many expressions");
                        word.SkipToKeyword(";");
                        return null;
//                        expression = Expressions.Expression.ParseCreate(word, nameSpace);
                    }
                    else if (portCount > task.PortsList.Count)
                    {
                        expression = Expressions.Expression.ParseCreate(word, nameSpace);
                    }
                    else
                    {
                        Verilog.DataObjects.Port port = task.PortsList[portCount];
                        if (port.Direction == DataObjects.Port.DirectionEnum.Input)
                        {
                            expression = Expressions.Expression.ParseCreate(word, nameSpace);
                        }
                        else
                        {
                            expression = Expressions.Expression.ParseCreateVariableLValue(word, nameSpace);
                        }
                    }

                    if (expression == null)
                    {
                        word.AddError("missed expression");
                        word.SkipToKeyword(";");
                        return null;
                    }
                    if (word.Text == ")")
                    {
                        break;
                    }
                    else if (word.Text == ",")
                    {
                        word.MoveNext();
                        portCount++;
                        continue;
                    }
                    else
                    {
                        word.AddError("illegal expression");
                        return null;
                    }
                }
                if (word.Text == ")") word.MoveNext();
                else word.AddError(") required");
            }
            else
            {
                if (task != null && task.Ports.Count != 0) word.AddError("missing ports.");
            }

            if (word.Text == ";")
            {
                word.MoveNext();
            }
            else word.AddError("; required");

            return taskEnable;

        }


    }
}
