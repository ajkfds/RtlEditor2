using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class ConditionalExpression : Expression
    {
        public Expression ConditionExpression;
        public Expression TrueExpression;
        public Expression FalseExpression;


        protected ConditionalExpression() { }
        // conditional_expression::= expression1 ? { attribute_instance } expression2: expression3

        public new ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();

            label.AppendLabel(this.ConditionExpression.GetLabel());
            label.AppendText(" ? ");
            label.AppendLabel(TrueExpression.GetLabel());
            label.AppendText(" : ");
            label.AppendLabel(FalseExpression.GetLabel());

            return label;
        }

        public static ConditionalExpression ParseCreate(WordScanner word,NameSpace nameSpace,Expression conditionExpresstion)
        {
            if (word.GetCharAt(0) != '?') return null;
            word.MoveNext();

            ConditionalExpression conditionalExpression = new ConditionalExpression();
            conditionalExpression.TrueExpression = Expression.ParseCreate(word, nameSpace);
            if(conditionalExpression.TrueExpression == null)
            {
                word.AddError("illegal conditional expression");
                return null;
            }

            if(word.GetCharAt(0) == ':')
            {
                word.MoveNext();
            }
            else
            {
                word.MoveNext();
            }

            conditionalExpression.FalseExpression = Expression.ParseCreate(word, nameSpace);
            if (conditionalExpression.FalseExpression == null)
            {
                word.AddError("illegal conditional expression");
                return null;
            }

            return conditionalExpression;
        }
    }
}
