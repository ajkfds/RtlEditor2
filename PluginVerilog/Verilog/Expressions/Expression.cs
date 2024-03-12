using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class Expression
    {
        protected Expression()
        {
            Constant = false;
        }

//        public List<Primary> RpnPrimarys = new List<Primary>();

        public Primary Primary;
        public virtual bool Constant { get; protected set; }
        public virtual double? Value { get; protected set; }
        public virtual int? BitWidth { get; protected set; }
        public WordReference Reference { get; protected set; }
        public bool IncrementDecrement = false;

        /// <summary>
        /// dispose refrence
        /// </summary>
        public virtual void DisposeReference()
        {
            Reference.Dispose();
            Reference = null;
        }

        /// <summary>
        ///  dispose reference hierarchy, keep top level reference only.
        /// </summary>
        public virtual void DisposeSubRefrence(bool keepThisReference)
        {
            if (!keepThisReference) DisposeReference();
        }

        /// <summary>
        /// get label object of this expression
        /// </summary>
        /// <returns></returns>
        public virtual ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            AppendLabel(label);
            return label;
        }
        public virtual string CreateString()
        {
            return null;
        }
        public virtual void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {

        }

        public virtual void AppendString(StringBuilder stringBuilder)
        {

        }

        public string ConstantValueString()
        {
            if (Constant)
            {
                if (Value == null)
                {
                    return "";
                }
                else
                {
                    return ((double)Value).ToString();
                }
            }
            else
            {
                return "";
            }
        }

        /*
        A.8.3 Expressions
        base_expression                 ::= expression
        expression1                     ::= expression 
        expression2                     ::= expression 
        expression3                     ::= expression 
        conditional_expression          ::= expression1 ? { attribute_instance } expression2 : expression3

        expression                      ::= primary 
                                            | unary_operator { attribute_instance } primary 
                                            | expression binary_operator { attribute_instance } expression 
                                            | conditional_expression 
                                            | string  

        constant_base_expression        ::= constant_expression
        constant_expression             ::= constant_primary          | unary_operator { attribute_instance } constant_primary          | constant_expression binary_operator { attribute_instance } constant_expression          | constant_expression ? { attribute_instance } constant_expression : constant_expression          | string
        constant_mintypmax_expression   ::= constant_expression          | constant_expression : constant_expression : constant_expression
        constant_range_expression       ::= constant_expression        | msb_constant_expression : lsb_constant_expression          | constant_base_expression +: width_constant_expression          | constant_base_expression -: width_constant_expression
        dimension_constant_expression   ::= constant_expression  
        lsb_constant_expression         ::= constant_expression  
        mintypmax_expression            ::= expression | expression : expression : expression  
        module_path_conditional_expression  ::= module_path_expression ? { attribute_instance } module_path_expression : module_path_expression 
        module_path_expression              ::= module_path_primary | unary_module_path_operator { attribute_instance } module_path_primary  | module_path_expression binary_module_path_operator { attribute_instance }                  module_path_expression          | module_path_conditional_expression 
        module_path_mintypmax_expression    ::= module_path_expression | module_path_expression : module_path_expression : module_path_expression
        msb_constant_expression         ::= constant_expression  
        range_expression                ::= expression  msb_constant_expression : lsb_constant_expression | base_expression +: width_constant_expression | base_expression -: width_constant_expression
        width_constant_expression ::= constant_expression
        */
        public static Expression ParseCreate(WordScanner word,NameSpace nameSpace)
        {
            Expression expression = new Expression();
            List<Operator> operatorsStock = new List<Operator>();
            WordReference reference = word.GetReference();

            List<Primary> rpnPrimarys = new List<Primary>();

            parseExpression(word, nameSpace, rpnPrimarys, operatorsStock,ref reference);
            expression.Reference = reference;
            while(operatorsStock.Count != 0)
            {
                rpnPrimarys.Add(operatorsStock.Last());
                operatorsStock.RemoveAt(operatorsStock.Count - 1);
            }
            if(rpnPrimarys.Count == 0)
            {
                return null;
            }

            if (rpnPrimarys.Count == 1 && rpnPrimarys[0] is Primary)
            {
                Primary primary = rpnPrimarys[0] as Primary;
                //expression.Constant = primary.Constant;
                //expression.Value = primary.Value;
                //expression.BitWidth = primary.BitWidth;
                //expression.Primary = primary;
                return primary;
            }

            if (!word.Active) return expression;

            bool incdec = false;
            if(rpnPrimarys.Count == 2)
            {
                if (rpnPrimarys[0] is IncDecOperator || rpnPrimarys[1] is IncDecOperator)
                {
                    incdec = true;
                }
            }


            // parse rpn
            List<Primary> primarys = new List<Primary>();
            for(int i=0;i<rpnPrimarys.Count;i++)
            {
                Primary item = rpnPrimarys[i];
                if (item is BinaryOperator)
                {
                    if (primarys.Count < 2) return null;
                    BinaryOperator op = item as BinaryOperator;
                    Primary primary = op.Operate(primarys[primarys.Count - 2], primarys[primarys.Count-1]);
                    primarys.RemoveAt(primarys.Count - 1);
                    primarys.RemoveAt(primarys.Count - 1);
                    primarys.Add(primary);
                }
                else if (item is UnaryOperator)
                {
                    if (primarys.Count < 1) return null;
                    UnaryOperator op = item as UnaryOperator;
                    Primary primary = op.Operate(primarys[0]);
                    primarys.RemoveAt(0);
                    primarys.Add(primary);
                }
                else if (item is IncDecOperator)
                {
                    if (primarys.Count < 1) return null;
                    IncDecOperator op = item as IncDecOperator;
                    Primary primary = op.Operate(primarys[0]);
                    primarys.RemoveAt(0);
                    primarys.Add(primary);
                }
                else if (item is TenaryOperator)
                {
                    if (primarys.Count < 3) return null;
                    TenaryOperator op = item as TenaryOperator;
                    Primary primary = op.Operate(primarys[primarys.Count - 3], primarys[primarys.Count - 2], primarys[primarys.Count - 1]);
                    primarys.RemoveAt(primarys.Count - 1);
                    primarys.RemoveAt(primarys.Count - 1);
                    primarys.RemoveAt(primarys.Count - 1);
                    primarys.Add(primary);
                }
                else
                {
                    primarys.Add(item as Primary);
                }
            }
            if(primarys.Count == 1)
            {
                //expression.Constant = Primarys[0].Constant;
                //expression.BitWidth = Primarys[0].BitWidth;
                //expression.Value = Primarys[0].Value;
                //expression.Primary = Primarys[0];
            }
            else
            {
                return null;
            }

            primarys[0].IncrementDecrement = incdec;
            return primarys[0];
//            return expression;
        }

        public static Expression CreateTempExpression(string text)
        {
            Expression expression = new Expression();
            expression.Primary = new TempPrimary(text);
            return expression;
        }


        // parse lvalue expression or task reference
        public static Expression ParseCreateVariableLValue(WordScanner word, NameSpace nameSpace)
        {
            Expression expression = new Expression();
            List<Operator> operatorsStock = new List<Operator>();
            List<Primary> rpnPrimarys = new List<Primary>();

            WordReference reference = word.GetReference();

            parseVariableLValue(word, nameSpace, rpnPrimarys, operatorsStock);
            expression.Reference = reference;
            while (operatorsStock.Count != 0)
            {
                rpnPrimarys.Add(operatorsStock.Last());
                operatorsStock.RemoveAt(operatorsStock.Count - 1);
            }
            if (rpnPrimarys.Count == 0)
            {
                return null;
            }

            if (rpnPrimarys.Count == 1 && rpnPrimarys[0] is Primary)
            {
                Primary primary = rpnPrimarys[0] as Primary;
                //expression.Constant = primary.Constant;
                //expression.Value = primary.Value;
                //expression.BitWidth = primary.BitWidth;
                //expression.Primary = primary;
                //return expression;
                return primary;
            }
            // parse rpn
            List<Primary> Primarys = new List<Primary>();
            for (int i = 0; i < rpnPrimarys.Count; i++)
            {
                Primary item = rpnPrimarys[i];
                if (item is Primary)
                {
                    Primarys.Add(item as Primary);
                }
                else if (item is BinaryOperator)
                {
                    if (Primarys.Count < 2) return null;
                    BinaryOperator op = item as BinaryOperator;
                    Primary primary = op.Operate(Primarys[0], Primarys[1]);
                    Primarys.RemoveAt(0);
                    Primarys.RemoveAt(0);
                    Primarys.Add(primary);
                }
                else if (item is UnaryOperator)
                {
                    if (Primarys.Count < 1) return null;
                    UnaryOperator op = item as UnaryOperator;
                    Primary primary = op.Operate(Primarys[0]);
                    Primarys.RemoveAt(0);
                    Primarys.Add(primary);
                }else if(item is TenaryOperator)
                {
                    if (Primarys.Count < 3) return null;
                    TenaryOperator op = item as TenaryOperator;
                    Primary primary = op.Operate(Primarys[0], Primarys[1], Primarys[2]);
                    Primarys.RemoveAt(0);
                    Primarys.RemoveAt(0);
                    Primarys.RemoveAt(0);
                    Primarys.Add(primary);
                }
                else
                {
                    return null;
                }
            }
            if (Primarys.Count == 1)
            {
                return Primarys[0];
//                expression.Constant = Primarys[0].Constant;
//                expression.BitWidth = Primarys[0].BitWidth;
//                expression.Value = Primarys[0].Value;
            }
            else
            {
                return null;
            }

            return expression;
        }

        private static bool parseExpression(WordScanner word,NameSpace nameSpace,List<Primary> Primarys,List<Operator> operatorStock,ref WordReference reference)
        {
            reference = word.GetReference(reference);

            // ++(primary),--(primary)
            Primary primary = Primary.ParseCreate(word, nameSpace);
            if (primary != null)
            {
                Primarys.Add(primary);
                IncDecOperator incdecOperator = IncDecOperator.ParseCreate(word);
                if (incdecOperator != null)
                {
                    Primarys.Add(incdecOperator);
                }
            }
            else
            {
                // ++(primary),-(primary)
                IncDecOperator incdecOperator = IncDecOperator.ParseCreate(word);
                if(incdecOperator != null)
                {
                    if (word.Eof)
                    {
                        word.AddError("illegal unary Operator");
                        return false;
                    }
                    primary = Primary.ParseCreate(word, nameSpace);
                    if (primary == null)
                    {
                        word.AddError("illegal unary Operator");
                        return false;
                    }
                    Primarys.Add(primary);
                    Primarys.Add(incdecOperator);
                }
                else
                {
                    UnaryOperator unaryOperator = UnaryOperator.ParseCreate(word);
                    if (unaryOperator != null)
                    {
                        if (word.Eof)
                        {
                            word.AddError("illegal unary Operator");
                            return false;
                        }
                        primary = Primary.ParseCreate(word, nameSpace);
                        if (primary == null)
                        {
                            word.AddError("illegal unary Operator");
                            return false;
                        }
                        Primarys.Add(primary);
                        Primarys.Add(unaryOperator);
//                        addOperator(unaryOperator, Primarys, operatorStock);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            if (word.Eof) return true;


            if (word.GetCharAt(0) == '?')
            {
                word.MoveNext();
                do
                {
                    if (!parseExpression(word, nameSpace, Primarys, operatorStock,ref reference))
                    {
                        word.AddError("illegal binary Operator");
                        break;
                    }
                    if (word.GetCharAt(0) == ':')
                    {
                        word.MoveNext();
                    }
                    else
                    {
                        word.AddError(": expected");
                        break;
                    }
                    if (!parseExpression(word, nameSpace, Primarys, operatorStock, ref reference))
                    {
                        word.AddError("illegal binary Operator");
                        break;
                    }
                    Primarys.Add(TenaryOperator.Create());
                } while (false);
            }

            BinaryOperator binaryOperator = BinaryOperator.ParseCreate(word);
            if (binaryOperator == null) return true;

            addOperator(binaryOperator, Primarys, operatorStock);

            if (!parseExpression(word, nameSpace, Primarys, operatorStock, ref reference))
            {
                word.AddError("illegal binary Operator");
            }

            return true;
        }

        private static bool parseVariableLValue(WordScanner word, NameSpace nameSpace, List<Primary> Primarys, List<Operator> operatorStock)
        {
            Primary primary = Primary.ParseCreateLValue(word, nameSpace);
            if (primary != null)
            {
                Primarys.Add(primary);
            }
            return true;
        }

        private static void addOperator(Operator newOperator,List<Primary> expressioItems, List<Operator> operatorStock)
        {
            while (operatorStock.Count != 0 && operatorStock.Last().Precedence <= newOperator.Precedence)
            {
                Operator popOperator = operatorStock.Last();
                operatorStock.RemoveAt(operatorStock.Count - 1);
                expressioItems.Add(popOperator);
            }
            operatorStock.Add(newOperator);
        }

    }

}
