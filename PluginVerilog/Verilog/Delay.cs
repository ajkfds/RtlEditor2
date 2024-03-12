using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    // A.2.2.3 Delays
    // delay3 ::= # delay_value | # ( delay_value [ , delay_value [ , delay_value ] ] )  
    // delay2 ::= # delay_value | # ( delay_value [ , delay_value ] )
    // delay_value ::=           unsigned_number         | parameter_identifier         | specparam_identifier         | mintypmax_expression

    public class Delay2
    {
        public Expressions.Expression DelayValue0 { get; protected set; }
        public Expressions.Expression DelayValue1 { get; protected set; }

        public static Delay2 ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "#") return null;
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            Delay2 delay = new Delay2();
            Expressions.Expression expression;

            expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null) return null;
            delay.DelayValue0 = expression;

            if (word.Text != ",") return delay;
            word.MoveNext();

            expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null) return null;
            delay.DelayValue1 = expression;

            return delay;
        }

        private static Expressions.Expression parseCreateDelayValue(WordScanner word, NameSpace nameSpace)
        {
            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null)
            {
                word.AddError("illegal delay value");
                return null;
            }
            if (!expression.Constant)
            {
                word.AddError("should be constant");
            }
            return expression;
        }
    }

    public class Delay3
    {
        public Expressions.Expression DelayValue0 { get; protected set; }
        public Expressions.Expression DelayValue1 { get; protected set; }
        public Expressions.Expression DelayValue2 { get; protected set; }

        public static Delay3 ParseCreate(WordScanner word, NameSpace nameSpace)
        {
            if (word.Text != "#") return null;
            word.Color(CodeDrawStyle.ColorType.Keyword);
            word.MoveNext();

            Delay3 delay = new Delay3();
            Expressions.Expression expression;

            expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null) return null;
            delay.DelayValue0 = expression;

            if (word.Text != ",") return delay;
            word.MoveNext();

            expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null) return null;
            delay.DelayValue1 = expression;

            if (word.Text != ",") return delay;
            word.MoveNext();

            expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null) return null;
            delay.DelayValue2 = expression;

            return delay;
        }

        private static Expressions.Expression parseCreateDelayValue(WordScanner word, NameSpace nameSpace)
        {
            Expressions.Expression expression = Expressions.Expression.ParseCreate(word, nameSpace);
            if (expression == null)
            {
                word.AddError("illegal delay value");
                return null;
            }
            if (!expression.Constant)
            {
                word.AddError("should be constant");
            }
            return expression;
        }
    }
}
