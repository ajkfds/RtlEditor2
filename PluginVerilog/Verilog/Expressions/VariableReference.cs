using pluginVerilog.Verilog.DataObjects.Nets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class VariableReference : Primary
    {
        protected VariableReference() { }
        public string VariableName { get; protected set; }
        public RangeExpression RangeExpression { get; protected set; }
        public List<Expression> Dimensions = new List<Expression>();
        public DataObjects.DataObject Variable = null;

        public override string CreateString()
        {
            return GetLabel().CreateString();
        }
        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            if (Variable is DataObjects.Variables.Reg)
            {
                label.AppendText(VariableName, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Register));
            }
            else if (Variable is Net)
            {
                label.AppendText(VariableName, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Net));
            }
            else
            {
                label.AppendText(VariableName);
            }
            if (RangeExpression != null)
            {
                label.AppendText(" ");
                label.AppendLabel(RangeExpression.GetLabel());
            }
            foreach (Expression expression in Dimensions)
            {
                label.AppendText(" [");
                label.AppendLabel(expression.GetLabel());
                label.AppendText("]");
            }
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(CreateString());
        }


        private static DataObjects.DataObject getDataObject(WordScanner word, string identifier, NameSpace nameSpace)
        {
            if (nameSpace.DataObjects.ContainsKey(identifier))
            {
                return nameSpace.DataObjects[identifier];
            }

            if (nameSpace is Function)
            {
                if (nameSpace.Parent != null)
                {
                    DataObjects.DataObject val = nameSpace.Parent.GetDataObject(identifier);
                    if (val != null) word.AddWarning("external function reference");
                    return val;
                }
            }
            else
            {
                if (nameSpace.Parent != null)
                {
                    return nameSpace.Parent.GetDataObject(identifier);
                }
                else
                {
                    return nameSpace.GetDataObject(identifier);
                }
            }
            return null;
        }

        public new static VariableReference ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            throw new Exception("illegal access");
        }

        public static VariableReference Create(DataObjects.Variables.Variable variable,NameSpace nameSpace)
        {
            VariableReference val = new VariableReference();
            val.VariableName = variable.Name;
            val.Variable = variable;
            val.Reference = variable.DefinedReference;

            return val;
        }

        public static VariableReference ParseCreate(WordScanner word, NameSpace nameSpace, bool assigned)
        {
            if (nameSpace == null) System.Diagnostics.Debugger.Break();
            DataObjects.DataObject variable = getDataObject(word, word.Text, nameSpace);
            if (variable == null) return null;

            VariableReference val = new VariableReference();
            val.VariableName = variable.Name;
            val.Variable = variable;
            val.Reference = word.GetReference();

            if (variable is DataObjects.Variables.Reg)
            {
                word.Color(CodeDrawStyle.ColorType.Register);
            }
            else if (variable is Net)
            {
                word.Color(CodeDrawStyle.ColorType.Net);
            }
            else
            {
                word.Color(CodeDrawStyle.ColorType.Variable);
            }

            if (assigned)
            {
                val.Variable.AssignedReferences.Add(word.GetReference());
            }
            else
            {
                val.Variable.UsedReferences.Add(word.GetReference());
            }

            word.MoveNext();

            while (!word.Eof && val.Dimensions.Count < variable.Dimensions.Count)
            {
                if (word.GetCharAt(0) != '[')
                {
                    word.AddError("lacked dimension");
                    break;
                }
                word.MoveNext();
                Expression exp = Expression.ParseCreate(word, nameSpace);
                val.Dimensions.Add(exp);
                if (word.GetCharAt(0) != ']')
                {
                    word.AddError("illegal dimension");
                    break;
                }
                word.MoveNext();
            }

            if (word.GetCharAt(0) == '[')
            {
                word.MoveNext();

                Expression exp1 = Expression.ParseCreate(word, nameSpace);
                Expression exp2;
                switch (word.Text)
                {
                    case ":":
                        word.MoveNext();
                        exp2 = Expression.ParseCreate(word, nameSpace);
                        if (word.Text != "]")
                        {
                            word.AddError("illegal range");
                            return null;
                        }
                        word.MoveNext();
                        val.RangeExpression = new AbsoluteRangeExpression(exp1, exp2);
                        break;
                    case "+:":
                        word.MoveNext();
                        exp2 = Expression.ParseCreate(word, nameSpace);
                        if (word.Text != "]")
                        {
                            word.AddError("illegal range");
                            return null;
                        }
                        word.MoveNext();
                        val.RangeExpression = new RelativePlusRangeExpression(exp1, exp2);
                        break;
                    case "-:":
                        word.MoveNext();
                        exp2 = Expression.ParseCreate(word, nameSpace);
                        if (word.Text != "]")
                        {
                            word.AddError("illegal range");
                            return null;
                        }
                        word.MoveNext();
                        val.RangeExpression = new RelativeMinusRangeExpression(exp1, exp2);
                        break;
                    case "]":
                        word.MoveNext();
                        val.RangeExpression = new SingleBitRangeExpression(exp1);
                        break;
                    default:
                        word.AddError("illegal range/dimension");
                        return null;
                }
                val.BitWidth = val.RangeExpression.BitWidth;
            }
            else
            {   // w/o range
                if (variable is DataObjects.Variables.Reg)
                {
                    DataObjects.Variables.Reg reg = (DataObjects.Variables.Reg)variable;
                    if (reg.Range != null) val.BitWidth = reg.Range.BitWidth;
                    else val.BitWidth = 1;
                }
                else if (variable is Net)
                {
                    if (((Net)variable).Range != null) val.BitWidth = ((Net)variable).Range.BitWidth;
                    else val.BitWidth = 1;
                }
                else if (variable is DataObjects.Variables.Genvar)
                {
                    val.Constant = true;
                }
            }

            return val;
        }

    }
}
