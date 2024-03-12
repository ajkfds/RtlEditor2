using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects
{
    public class Range
    {
        protected Range() { }

        // ## SystemVerilog 2017
        // unpacked_dimension       ::= [constant_range] | [constant_expression]
        // packed_dimension         ::= [constant_range] | unsized_dimension
        // associative_dimension    ::= [data_type] | [ * ]
        // variable_dimension       ::= unsized_dimension| unpacked_dimension | associative_dimension | queue_dimension
        // queue_dimension          ::= [ $ [ : constant_expression] ]
        // unsized_dimension        ::= [ ]
        // onstant_range ::= constant_expression : constant_expression 

        public Expressions.Expression MsbBitExpression { get; protected set; }
        public Expressions.Expression LsbBitExpression { get; protected set; }
        public int? BitWidth { get; protected set; }
        public bool Constant { get; protected set; }

        public string CreateString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            sb.Append(MsbBitExpression.CreateString());
            sb.Append(":");
            sb.Append(LsbBitExpression.CreateString());
            sb.Append("]");
            return sb.ToString();
        }

        public void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText("[");
            label.AppendLabel(MsbBitExpression.GetLabel());
            label.AppendText(":");
            label.AppendLabel(LsbBitExpression.GetLabel());
            label.AppendText("]");
        }


        public ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            AppendLabel(label);
            return label;
        }

        /*
        A.2.5 Declaration ranges
        dimension ::= [ dimension_constant_expression : dimension_constant_expression ]
        range ::= [ msb_constant_expression : lsb_constant_expression ]  
        constant_expression ::= (From Annex A - A.8.3)
        constant_primary | unary_operator { attribute_instance } constant_primary | constant_expression binary_operator { attribute_instance } constant_expression | constant_expression ? { attribute_instance } constant_expression     constant_expression | string 
         */
        public static Range ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.GetCharAt(0) != '[') System.Diagnostics.Debugger.Break();
            word.MoveNext(); // [

            Expressions.Expression msbExpression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (word.Eof || msbExpression == null)
            {
                word.AddError("illegal range");
                return null;
            }
            if (word.GetCharAt(0) == ']')
            {
                word.MoveNext();
                Range range = new Range();
                range.MsbBitExpression = msbExpression;
                range.LsbBitExpression = msbExpression;
                range.BitWidth = 1;
                range.Constant = msbExpression.Constant;
                return range;
            }

            if (word.GetCharAt(0) != ':' || word.Length != 1)
            {
                word.AddError("illegal range");
                return null;
            }
            word.MoveNext(); // :
            if (word.Eof)
            {
                word.AddError("illegal range");
                return null;
            }
            Expressions.Expression lsbExpression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (word.Eof || lsbExpression == null)
            {
                word.AddError("illegal range");
                return null;
            }
            if (word.GetCharAt(0) != ']')
            {
                word.AddError("illegal range");
                return null;
            }
            word.MoveNext(); // [
            {
                Range range = new Range();
                range.MsbBitExpression = msbExpression;
                range.LsbBitExpression = lsbExpression;

                if (msbExpression.Value != null && lsbExpression.Value != null)
                {
                    range.BitWidth = (int)msbExpression.Value - (int)lsbExpression.Value + 1;
                }


                if (msbExpression.Constant && lsbExpression.Constant)
                {
                    range.Constant = true;
                }
                else
                {
                    range.Constant = false;
                }


                return range;
            }
        }
    }
}
