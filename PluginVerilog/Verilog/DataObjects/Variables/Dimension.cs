using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.DataObjects.Variables
{
    public class Dimension
    {
        protected Dimension() { }





        public Expressions.Expression MsbBitExpression { get; protected set; }
        public Expressions.Expression LsbBitExpression { get; protected set; }
        /*
        A.2.5 Declaration ranges
        dimension ::= [ dimension_constant_expression : dimension_constant_expression ]
        range ::= [ msb_constant_expression : lsb_constant_expression ]  
        constant_expression ::= (From Annex A - A.8.3)
        constant_primary | unary_operator { attribute_instance } constant_primary | constant_expression binary_operator { attribute_instance } constant_expression | constant_expression ? { attribute_instance } constant_expression     constant_expression | string 
         */
        public ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            label.AppendText("[");
            label.AppendLabel(MsbBitExpression.GetLabel());
            label.AppendText(":");
            label.AppendLabel(LsbBitExpression.GetLabel());
            label.AppendText("]");
            return label;
        }

        public static Dimension ParseCreate(WordScanner word, NameSpace nameSpace)
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
                Dimension range = new Dimension();
                range.MsbBitExpression = msbExpression;
                range.LsbBitExpression = msbExpression;

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
                Dimension range = new Dimension();
                range.MsbBitExpression = msbExpression;
                range.LsbBitExpression = lsbExpression;

                return range;
            }
        }
    }
}
