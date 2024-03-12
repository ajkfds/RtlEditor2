using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class MinTypMax : Primary
    {
        protected MinTypMax() { }
        public Expression MinExpression { get; protected set; }
        public Expression TypExpression { get; protected set; }
        public Expression MaxExpression { get; protected set; }

        public static MinTypMax ParseCreate(WordScanner word, NameSpace nameSpace, Expression minExpresstion)
        {
            word.MoveNext();
            if (word.Eof)
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            Expression exp2 = Expression.ParseCreate(word, nameSpace);
            if (exp2 == null)
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            if (word.Eof)
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            if (word.GetCharAt(0) != ':')
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            word.MoveNext();
            if (word.Eof)
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            Expression exp3 = Expression.ParseCreate(word, nameSpace);
            if (exp3 == null)
            {
                word.AddError("illegal MinTypMax");
                return null;
            }
            if (word.Eof | word.GetCharAt(0) != ')')
            {
                word.AddError("illegal MinTypMax");
                return null;
            }

            MinTypMax minTypMax = new MinTypMax();
            minTypMax.MinExpression = minExpresstion;
            minTypMax.TypExpression = exp2;
            minTypMax.MaxExpression = exp3;

            return minTypMax;
        }
    }
}
