using pluginVerilog.Verilog.Expressions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class Operator : Primary
    {
        protected Operator(string text,byte precedence)
        {
            this.Text = text;
            this.Precedence = precedence;
        }

        public readonly string Text = "";
        public readonly byte Precedence;

        public override ajkControls.ColorLabel.ColorLabel GetLabel()
        {
            ajkControls.ColorLabel.ColorLabel label = new ajkControls.ColorLabel.ColorLabel();
            AppendLabel(label);
            return label;
        }

        public override string CreateString()
        {
            return Text;
        }
    }
    /*
    Table 12—Precedence rules for operators
    1   + - ! ~ (unary)             Highest precedence
    2   **
    3   *  /  %
    4   +  - (binary)
    5   <<  >>  <<<  >>>
    6   <  <=  >  >=
    7   ==  !=  ===  !==
    8   &  ~&
    9   ^  ^~  ~^
    10  |  ~|
    11  &&
    12  ||
    13  ?: (conditional operator)   Lowest precedence
    */

    /* SystemVerilog2017
     * 
    Precedence rules for operators
    1   () [] :: .                                              Left    Highest
    2                                  (' cast)
    3   + - ! ~ & ~& | ~| ^ ~^ ^~ ++ -- (unary)
    4   **                                                      Left
    5   * / %                                                   Left
    6   + - (binary)                                            Left
    7   << >> <<< >>>                                           Left
    8   < <= > >= inside dist                                   Left
    9   == != === !== ==? !=?                                   Left
    10   & (binary)                                              Left
    11  ^ ~^ ^~ (binary)                                        Left
    12  | (binary)                                              Left
    13  &&                                                      Left
    14  ||                                                      Left
    15  ?: (conditional operator)                               Right
    16  –> <–>                                                  Right
    17  = += -= *= /= %= &= ^= |= <<= >>= <<<= >>>= := :/ <=    None
    18  {} {{}}                                                 Concatenation   Lowest

    assignment_operator ::= = | += | -= | *= | /= | %= | &= | |= | ^= | <<= | >>= | <<<= | >>>=
    conditional_expression ::= cond_predicate ? { attribute_instance } expression : expression 
    binary_operator ::=  + | - | * | / | % | == | != | === | !== | ==? | !=? | && | || | **
    | < | <= | > | >= | & | | | ^ | ^~ | ~^ | >> | << | >>> | <<<
    | -> | <-> 
    inc_or_dec_operator ::= ++ | --
    stream_operator ::= >> | <<
    */


    /* SystemVerilog2017 not implemented list
    Precedence rules for operators
    1   () [] :: .                                              Left    Highest
    2                                  (' cast)
    8  inside dist                                   Left
    17  = += -= *= /= %= &= ^= |= <<= >>= <<<= >>>= := :/ <=    None
    18  {} {{}}                                                 Concatenation   Lowest

    assignment_operator ::= = | += | -= | *= | /= | %= | &= | |= | ^= | <<= | >>= | <<<= | >>>=
    conditional_expression ::= cond_predicate ? { attribute_instance } expression : expression 
    inc_or_dec_operator ::= ++ | --
    stream_operator ::= >> | <<
    */

    public class TenaryOperator : Operator
    {
        protected TenaryOperator(string text, byte precedence) : base(text, precedence) { }

        public static TenaryOperator Create()
        {
            return new TenaryOperator("?",0);
        }

        public override void DisposeSubRefrence(bool keepThisReference)
        {
            base.DisposeSubRefrence(keepThisReference);
            Condition.DisposeSubRefrence(false);
            Primary1.DisposeSubRefrence(false);
            Primary2.DisposeSubRefrence(false);
        }

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            Condition.AppendLabel(label);
            label.AppendText("?");
            Primary1.AppendLabel(label);
            label.AppendText(":");
            Primary2.AppendLabel(label);
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            Condition.AppendString(stringBuilder);
            stringBuilder.Append("?");
            Primary1.AppendString(stringBuilder);
            stringBuilder.Append(":");
            Primary2.AppendString(stringBuilder);
        }

        public delegate void OperatedAction(TenaryOperator tenaryOperator);
        public static OperatedAction Operated;

        public Primary Condition;
        public Primary Primary1;
        public Primary Primary2;
        public TenaryOperator Operate(Primary condition, Primary primary1, Primary primary2)
        {
            Condition = condition;
            Primary1 = primary1;
            Primary2 = primary2;

            bool constant = false;
            double? value = null;
            int? bitWidth = null;

            if (primary1.Constant && primary2.Constant & condition.Constant ) constant = true;
            if (condition.Value != null && primary1.Value != null && primary2.Value != null) value = getValue((double)condition.Value, (double)primary1.Value, (double)primary2.Value);
            if (primary1.BitWidth != null && primary2.BitWidth != null) bitWidth = getBitWidth(Text, (int)primary1.BitWidth, (int)primary2.BitWidth);

            this.Constant = constant;
            this.Value = value;
            this.BitWidth = BitWidth;
            if(Primary2.Reference != null && Condition.Reference != null)
            {
                this.Reference = Primary2.Reference.CreateReferenceFrom(Condition.Reference);
            }
            if (Operated!=null) Operated(this);
            return this;
        }

        private int? getBitWidth(string operatorText, int bitWidth1, int bitWidth2)
        {
            int maxWidth = bitWidth1;
            if (bitWidth2 > bitWidth1) maxWidth = bitWidth2;


            return bitWidth1;
        }

        private double? getValue(double condition, double value1, double value2)
        {
            if(condition != 0)
            {
                return value1;
            }
            else
            {
                return value2;
            }
        }
    }

    public class IncDecOperator : Operator
    {
        protected IncDecOperator(WordScanner word, string text, byte precedence) : base(text, precedence)
        {
            Reference = word.GetReference();
            word.MoveNext();
        }
        /*
        inc_or_dec_operator ::= ++ | --
        */

        public override void DisposeSubRefrence(bool keepThisReference)
        {
            base.DisposeSubRefrence(keepThisReference);
            Primary.DisposeSubRefrence(false);
        }

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Text);
            Primary.AppendLabel(label);
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(Text);
            Primary.AppendString(stringBuilder);
        }

        public static IncDecOperator ParseCreate(WordScanner word)
        {
            switch (word.Length)
            {
                case 2:
                    if (word.GetCharAt(0) == '+')
                    {
                        if (word.GetCharAt(1) == '+') { return new IncDecOperator(word, "++", 3); }
                        return null;
                    }
                    else if (word.GetCharAt(0) == '-')
                    {
                        if (word.GetCharAt(1) == '-') { return new IncDecOperator(word, "--", 3); }
                        return null;
                    }
                    else
                    {
                        return null;
                    }
                default:
                    return null;
            }
        }

        public delegate void OperatedAction(IncDecOperator unaryOperator);
        public static OperatedAction Operated;

        public IncDecOperator Operate(Primary primary)
        {
            Primary = primary;
            bool constant = false;
            double? value = null;
            int? bitWidth = null;

            if (primary.Constant) constant = true;
            if (primary.Value != null) value = getValue(Text, (double)primary.Value);
            if (primary.BitWidth != null) bitWidth = getBitWidth(Text, (int)primary.BitWidth);

            this.Constant = constant;
            this.Value = value;
            this.BitWidth = BitWidth;
            if (Primary.Reference != null)
            {
                this.Reference = Primary.Reference.CreateReferenceFrom(Primary.Reference);
            }
            if (Operated != null) Operated(this);
            return this;
        }

        private double? getValue(string text, double value)
        {
            switch (text)
            {
                case "++":
                    return value+1;
                case "--":
                    return value-1;
                default:
                    return null;
            }
        }

        private int? getBitWidth(string text, int bitWidth)
        {
            switch (text)
            {
                case "++":
                case "--":
                    return bitWidth + 1;
                default:
                    return null;
            }
        }
    }

    public class UnaryOperator : Operator
    {
        protected UnaryOperator(WordScanner word, string text,byte precedence) : base(text,precedence) 
        {
            Reference = word.GetReference();
            word.MoveNext();
        }
        /*
        unary_operator  ::=   + 
                            | - 
                            | ! 
                            | ~ 
                            | & 
                            | | 
                            | ^ 
                            | ~^ 
                            | ~& 
                            | ~| 
                            | ^~
        */

        public override void DisposeSubRefrence(bool keepThisReference)
        {
            base.DisposeSubRefrence(keepThisReference);
            Primary.DisposeSubRefrence(false);
        }

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Text);
            Primary.AppendLabel(label);
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(Text);
            Primary.AppendString(stringBuilder);
        }

        public static UnaryOperator ParseCreate(WordScanner word)
        {
            switch (word.Length)
            {
                case 1:
                    if (word.GetCharAt(0) == '+') { return new UnaryOperator(word,"+",3); }
                    if (word.GetCharAt(0) == '-') { return new UnaryOperator(word, "-",3); }
                    if (word.GetCharAt(0) == '!') { return new UnaryOperator(word, "!",3); }
                    if (word.GetCharAt(0) == '~') { return new UnaryOperator(word, "~",3); }
                    if (word.GetCharAt(0) == '&') { return new UnaryOperator(word, "&",3); }
                    if (word.GetCharAt(0) == '|') { return new UnaryOperator(word, "|",3); }
                    if (word.GetCharAt(0) == '^') { return new UnaryOperator(word, "^",3); }
                    return null;
                case 2:
                    if (word.GetCharAt(0) == '~')
                    {
                        if (word.GetCharAt(1) == '^') { return new UnaryOperator(word, "~^",3); }
                        if (word.GetCharAt(1) == '&') { return new UnaryOperator(word, "~&",3); }
                        if (word.GetCharAt(1) == '|') { return new UnaryOperator(word, "~|",3); }
                        return null;
                    }
                    else if (word.GetCharAt(0) == '^')
                    {
                        if (word.GetCharAt(1) == '~') { return new UnaryOperator(word, "^~", 3); }
                        return null;
                    }
                    else
                    {
                        return null;
                    }
                default:
                    return null;
            }
        }

        public delegate void OperatedAction(UnaryOperator unaryOperator);
        public static OperatedAction Operated;

        public UnaryOperator Operate(Primary primary)
        {
            Primary = primary;
            bool constant = false;
            double? value = null;
            int? bitWidth = null;

            if (primary.Constant) constant = true;
            if (primary.Value != null) value = getValue(Text, (double)primary.Value);
            if (primary.BitWidth != null) bitWidth = getBitWidth(Text, (int)primary.BitWidth);

            this.Constant = constant;
            this.Value = value;
            this.BitWidth = BitWidth;
            if(Primary.Reference != null)
            {
                this.Reference = Primary.Reference.CreateReferenceFrom(Primary.Reference);
            }
            if (Operated != null) Operated(this);
            return this;
        }

        private double? getValue(string text,double value)
        {
            switch (text)
            {
                // alithmetic operators
                case "+":
                    return value;
                case "-":
                    return -value;

                // logical negation
                case "!":
                    return null;

                // reduction operators
                case "&":
                case "|":
                case "^":
                case "~^":
                case "~&":
                case "~|":
                case "^~":
                    return null;

                default:
                    return null;
            }
        }

        private int? getBitWidth(string text,int bitWidth)
        {
            switch (text)
            {
                // alithmetic operators
                case "+":
                case "-":
                    return bitWidth+1;

                // logical negation
                case "!":
                    return bitWidth;

                // reduction operators
                case "&":
                case "|":
                case "^":
                case "~^":
                case "~&":
                case "~|":
                case "^~":
                    return 1;
                default:
                    return null;
            }
        }

    }
    public class BinaryOperator : Operator
    {
        protected BinaryOperator(WordScanner word, string text, byte precedence) : base(text, precedence) 
        {
            Reference = word.GetReference();
            word.MoveNext();
        }
        /*
        binary_operator ::=   + 
                            | - 
                            | * 
                            | / 
                            | %
                            | < 
                            | > 
                            | & 
                            | | 
                            | ^ 

                            | == 
                            | != 
                            | <= 
                            | >= 

                            | && 
                            | || 
                            | **
                            | >> 
                            | << 
                            | ^~ 
                            | ~^ 
                            | === 
                            | !== 
                            | >>> 
                            | <<< 
        */

        public override void DisposeSubRefrence(bool keepThisReference)
        {
            base.DisposeSubRefrence(keepThisReference);
            Primary1.DisposeSubRefrence(false);
            Primary2.DisposeSubRefrence(false);
        }
        public static BinaryOperator ParseCreate(WordScanner word)
        {

            switch (word.Length)
            {
                case 1:
                    if (word.GetCharAt(0) == '+') { return new BinaryOperator(word,"+",6); }
                    if (word.GetCharAt(0) == '-') { return new BinaryOperator(word, "-",6); }
                    if (word.GetCharAt(0) == '*') { return new BinaryOperator(word, "*",5); }
                    if (word.GetCharAt(0) == '/') { return new BinaryOperator(word, "/",5); }
                    if (word.GetCharAt(0) == '%') { return new BinaryOperator(word, "%",5); }
                    if (word.GetCharAt(0) == '<') { return new BinaryOperator(word, "<",8); }
                    if (word.GetCharAt(0) == '>') { return new BinaryOperator(word, ">",8); }
                    if (word.GetCharAt(0) == '&') { return new BinaryOperator(word, "&",10); }
                    if (word.GetCharAt(0) == '|') { return new BinaryOperator(word, "|",12); }
                    if (word.GetCharAt(0) == '^') { return new BinaryOperator(word, "^",11); }
                    if (word.GetCharAt(0) == '\'') { return new BinaryOperator(word, "\'", 2); }
                    return null;
                case 2:
                    if (word.GetCharAt(1) == '=')
                    {
                        if (word.GetCharAt(0) == '=') { return new BinaryOperator(word, "==",9); }
                        if (word.GetCharAt(0) == '!') { return new BinaryOperator(word, "!=",9); }
                        if (word.GetCharAt(0) == '<') { return new BinaryOperator(word, "<=",8); }
                        if (word.GetCharAt(0) == '>') { return new BinaryOperator(word, ">=",8); }
                        return null;
                    }
                    else if (word.GetCharAt(0) == word.GetCharAt(1))
                    {
                        if (word.GetCharAt(0) == '&') { return new BinaryOperator(word,"&&",13); }
                        if (word.GetCharAt(0) == '|') { return new BinaryOperator(word, "||",14); }
                        if (word.GetCharAt(0) == '*') { return new BinaryOperator(word, "**",4); }
                        if (word.GetCharAt(0) == '>') { return new BinaryOperator(word, ">>",7); }
                        if (word.GetCharAt(0) == '<') { return new BinaryOperator(word, "<<",7); }
                        if (word.GetCharAt(0) == '^' && word.GetCharAt(1) == '~') { return new BinaryOperator(word, "^~",11); }
                        if (word.GetCharAt(0) == '~' && word.GetCharAt(1) == '^') { return new BinaryOperator(word, "~^",11); }
                        return null;
                    }
                    if (word.GetCharAt(1) == '>')
                    {
                        if (word.GetCharAt(0) == '-') { return new BinaryOperator(word, "->", 16); }
                        return null;
                    }
                    else
                    {
                        return null;
                    }
                case 3:
                    if (word.GetCharAt(1) != word.GetCharAt(2))
                    {
                        if (word.GetCharAt(0) == '=' | word.GetCharAt(1) == '=' | word.GetCharAt(2) == '?') { return new BinaryOperator(word, "==?",9 ); }
                        if (word.GetCharAt(0) == '!' | word.GetCharAt(1) == '=' | word.GetCharAt(2) == '?') { return new BinaryOperator(word, "!=?",9 ); }
                        if (word.GetCharAt(0) == '<' | word.GetCharAt(1) == '-' | word.GetCharAt(2) == '>') { return new BinaryOperator(word, "<->", 16); }
                        return null;
                    }
                    if (word.GetCharAt(1) == '=')
                    {
                        if (word.GetCharAt(0) == '=') { return new BinaryOperator(word, "===",9); }
                        if (word.GetCharAt(0) == '!') { return new BinaryOperator(word, "!==",9); }
                        return null;
                    }
                    else if (word.GetCharAt(0) == word.GetCharAt(1))
                    {
                        if (word.GetCharAt(0) == '>') { return new BinaryOperator(word, ">>>",7); }
                        if (word.GetCharAt(0) == '<') { return new BinaryOperator(word, "<<<",7); }
                        return null;
                    }
                    else
                    {
                        return null;
                    }
                default:
                    return null;
            }
        }

        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            Primary1.AppendLabel(label);
            label.AppendText(Text);
            Primary2.AppendLabel(label);
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            Primary1.AppendString(stringBuilder);
            stringBuilder.Append(Text);
            Primary2.AppendString(stringBuilder);
        }


        Primary Primary1;
        Primary Primary2;

        public delegate void OperatedAction(BinaryOperator binaryOperator);
        public static OperatedAction Operated;

        public BinaryOperator Operate(Primary primary1, Primary primary2)
        {
            Primary1 = primary1;
            Primary2 = primary2;

            bool constant = false;
            double? value = null;
            int? bitWidth = null;

            if (primary1.Constant && primary2.Constant) constant = true;
            if (primary1.Value != null && primary2.Value != null) value = getValue(Text, (double)primary1.Value, (double)primary2.Value);
            if (primary1.BitWidth != null && primary2.BitWidth != null) bitWidth = getBitWidth(Text, (int)primary1.BitWidth, (int)primary2.BitWidth);

            this.Constant = constant;
            this.Value = value;
            this.BitWidth = bitWidth;
            Primary ret = this;
            if (Primary1 != null && Primary2.Reference != null)
            {
                this.Reference = Primary2.Reference.CreateReferenceFrom(Primary1.Reference);
            }
            //            Primary ret = Primary.Create(constant, value, bitWidth);
            if (Operated != null) Operated(this);
            return this;
        }


        private int? getBitWidth(string operatorText,int bitWidth1,int bitWidth2)
        {
            int maxWidth = bitWidth1;
            if (bitWidth2 > bitWidth1) maxWidth = bitWidth2;

            switch (operatorText)
            {
                // alithmetic operator
                case "+":
                    return maxWidth + 1;
                case "-":
                    return maxWidth;
                case "*":
                    return bitWidth1 + bitWidth2;
                case "/":
                    return maxWidth;
                case "**":
                    return bitWidth1 * bitWidth2;

                // modulus operator
                case "%":
                    return bitWidth2;

                // relational operators
                case "<":
                case ">":
                case "<=":
                case ">=":
                    return 1;
                // equality operators
                case "==":
                case "!=":
                case "===":
                case "!==":
                    return 1;
                // logical operators
                case "&&":
                case "||":
                    return 1;

                // bit-wise binary operators
                case "&":
                case "|":
                case "^":
                case "^~":
                case "~^":
                    return maxWidth;

                // logical shoft
                case ">>":
                case "<<":
                    return bitWidth1;
                // alithmetic shift
                case ">>>":
                case "<<<":
                    return bitWidth1;

                // cast
                case "\'":
                    return bitWidth1;

                default:
                    return null;
            }

        }

        private double? getValue(string operatorText, double value1, double value2)
        {
            switch (Text)
            {
                // alithmetic operator
                case "+":
                    return value1 + value2;
                case "-":
                    return value1 - value2;
                case "*":
                    return value1 * value2;
                case "/":
                    return value1 / value2;
                case "**":
                    return Math.Pow(value1, value2);

                // modulus operator
                case "%":
                    return value1 % value2;

                // relational operators
                case "<":
                    return (value1 < value2) ? 1:0;
                case ">":
                    return (value1 > value2) ? 1 : 0;
                case "<=":
                    return (value1 <= value2) ? 1 : 0;
                case ">=":
                    return (value1 >= value2) ? 1 : 0;

                // equality operators
                case "==":
                    return (value1 == value2) ? 1 : 0;
                case "!=":
                    return (value1 != value2) ? 1 : 0;
                case "===":
                    return (value1 == value2) ? 1 : 0;
                case "!==":
                    return (value1 != value2) ? 1 : 0;

                // logical operators
                case "&&":
                    return (value1 != 0) & (value2 != 0) ? 1 : 0;
                case "||":
                    return (value1 != 0) | (value2 != 0) ? 1 : 0;

                // bit-wise binary operators
                case "&":
                    {
                        ulong lng1 = (ulong)value1;
                        ulong lng2 = (ulong)value2;
                        return (lng1 & lng2);
                    }
                case "|":
                    {
                        ulong lng1 = (ulong)value1;
                        ulong lng2 = (ulong)value2;
                        return (lng1 | lng2);
                    }
                case "^":
                case "^~":
                case "~^":
                
                // logical shoft
                case ">>":
                case "<<":
                // alithmetic shift
                case ">>>":
                case "<<<":
                    return null;

                // cast
                case "\'":
                    return value1; 
                default:
                    return null;
            }
        }


    }

    }
