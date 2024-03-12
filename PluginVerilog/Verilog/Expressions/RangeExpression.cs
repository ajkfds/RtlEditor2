using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    //  range_expression ::=    expression        
    //                          | msb_constant_expression : lsb_constant_expression
    //                          | base_expression +: width_constant_expression
    //                          | base_expression -: width_constant_expression 
    //         | hierarchical_identifier          | hierarchical_identifier [ expression ] { [ expression ] }          | hierarchical_identifier [ expression ] { [ expression ] }  [ range_expression ]          | hierarchical_identifier [ range_expression ]  
    public class RangeExpression
    {
        public int BitWidth;
        public virtual ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            AppendLabel(label);
            return label;
        }

        public virtual void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
        }
    }
    public class SingleBitRangeExpression : RangeExpression
    {
        protected SingleBitRangeExpression() { }
        public SingleBitRangeExpression(Expression expression)
        {
            Expression = expression;
            BitWidth = 1;
        }
        public Expression Expression;
        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText("[");
            label.AppendLabel(Expression.GetLabel());
            label.AppendText("]");
        }
    }
    public class AbsoluteRangeExpression : RangeExpression
    {
        protected AbsoluteRangeExpression() { }
        public AbsoluteRangeExpression(Expression expression1, Expression expression2)
        {
            MsbExpression = expression1;
            LsbExpression = expression2;
            if (LsbExpression == null || MsbExpression == null) return;
            if(MsbExpression.Constant && LsbExpression.Constant && MsbExpression.Value != null && LsbExpression.Value != null)
            {
                BitWidth = (int)MsbExpression.Value - (int)LsbExpression.Value + 1;
            }
        }
        public Expression MsbExpression;
        public Expression LsbExpression;
        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            if (LsbExpression == null || MsbExpression == null) return;
            label.AppendText("[");
            label.AppendLabel(MsbExpression.GetLabel());
            label.AppendText(":");
            label.AppendLabel(LsbExpression.GetLabel());
            label.AppendText("]");
        }
    }
    public class RelativePlusRangeExpression : RangeExpression
    {
        protected RelativePlusRangeExpression() { }
        public RelativePlusRangeExpression(Expression expression1, Expression expression2)
        {
            BaseExpression = expression1;
            WidthExpression = expression2;
            if (WidthExpression.Constant && WidthExpression.Value != null)
            {
                BitWidth = (int)WidthExpression.Value;
            }
        }
        public Expression BaseExpression;
        public Expression WidthExpression;

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            if (BaseExpression == null || WidthExpression == null) return;
            label.AppendText("[");
            label.AppendLabel(BaseExpression.GetLabel());
            label.AppendText("+:");
            label.AppendLabel(WidthExpression.GetLabel());
            label.AppendText("]");
        }
    }
    public class RelativeMinusRangeExpression : RangeExpression
    {
        protected RelativeMinusRangeExpression() { }
        public RelativeMinusRangeExpression(Expression expression1, Expression expression2)
        {
            BaseExpression = expression1;
            WidthExpression = expression2;
            if (WidthExpression != null && WidthExpression.Constant && WidthExpression.Value != null)
            {
                BitWidth = (int)WidthExpression.Value;
            }
        }
        public Expression BaseExpression;
        public Expression WidthExpression;

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            if (BaseExpression == null || WidthExpression == null) return;
            label.AppendText("[");
            label.AppendLabel(BaseExpression.GetLabel());
            label.AppendText("-:");
            label.AppendLabel(WidthExpression.GetLabel());
            label.AppendText("]");
        }
    }
}
