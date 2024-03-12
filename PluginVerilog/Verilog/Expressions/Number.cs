using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class Number : Primary
    {
        protected Number() { }

        public override bool Constant
        {
            get
            {
                return true;
            }
        }

        // A.8.7 Numbers
        // number           ::= decimal_number
        //                      | octal_number
        //                      | binary_number
        //                      | hex_number
        //                      | real_number
        // real_number      ::= unsigned_number.unsigned_number
        //                      | unsigned_number[.unsigned_number] exp [sign] unsigned_number  
        // exp ::= e|E
        // sign ::= + | -

        // done!!   decimal_number  ::= unsigned_number 
        //                          | [size] decimal_base unsigned_number
        //                          | [size] decimal_base x_digit { _ }
        //                          | [size] decimal_base z_digit { _ }
        // done!!   binary_number    ::= [size] binary_base binary_value
        // done!!   octal_number     ::= [size] octal_base octal_value
        // done!!   hex_number       ::= [size] hex_base hex_value

        // size ::= non_zero_unsigned_number  

        // non_zero_unsigned_number ::= non_zero_decimal_digit { _ | decimal_digit} 

        // unsigned_number ::= decimal_digit { _ | decimal_digit }  

        // binary_value ::= binary_digit { _ | binary_digit }  
        // octal_value ::= octal_digit { _ | octal_digit }  
        // hex_value ::= hex_digit { _ | hex_digit }  


        // decimal_base ::= ’[s|S]d | ’[s|S]D
        // binary_base ::= ’[s|S]b |  ’[s|S]B  
        // octal_base ::= ’[s|S]o | ’[s|S]O
        // hex_base1 ::= ’[s|S]h | ’[s|S]H  
        // non_zero_decimal_digit ::= 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 
        // decimal_digit ::= 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9  
        // binary_digit ::= x_digit | z_digit | 0 | 1  
        // octal_digit ::= x_digit | z_digit | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7  
        // hex_digit ::=  x_digit | z_digit | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9          | a | b | c | d | e | f | A | B | C | D | E | F  
        // x_digit ::= x | X 
        // z_digit ::= z | Z | ?

        public bool Signed = false;
        public NumberTypeEnum NumberType = NumberTypeEnum.Decimal;
        public string Text = "";

        public enum NumberTypeEnum
        {
            Decimal,
            Binary,
            Octal,
            Hex
        }

        public override string CreateString()
        {
            return Text;
        }
        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Text, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Number));
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(Text);
        }

        public static Number ParseCreate(WordScanner word)
        {
            word.Color(CodeDrawStyle.ColorType.Number);
            

            Number number = new Number();
            int index = 0;
            int apostropheIndex = -1;
            number.Text = word.Text;
            number.Reference = word.GetReference();
            StringBuilder sb = new StringBuilder();

            // get string before ' and ' index

            while (index < word.Length)
            {
                if(word.GetCharAt(index) == '\'')
                {
                    apostropheIndex = index;
                    break;
                } 
                if(isDecimalDigit(word.GetCharAt(index)))
                {
                    sb.Append(word.GetCharAt(index));
                }
                else if ( word.GetCharAt(index) == '_')
                {
                    if(index == 0) return null;
                }
                else if (word.GetCharAt(index) == '.' || word.GetCharAt(index) == 'e' || word.GetCharAt(index) == 'E')
                { // real
                    if (!parseRealValueAfterInteger(number, word, ref index, sb))
                    {
                        word.AddError("illegal real value");
                        return null;
                    }
                    word.MoveNext();
                    return number;
                }
                else
                {
                    word.AddError("illegal number value");
                    return null;
                }
                index++;
            }

            if(apostropheIndex == -1)
            { // decimal
                number.NumberType = NumberTypeEnum.Decimal;
                number.Value = long.Parse(sb.ToString());
                number.Constant = true;
                word.MoveNext();

                if (!word.Text.StartsWith("\'")) return number;
                Number number2 = Number.ParseCreate(word);
                if(number2 != null && number != null) number2.BitWidth = number.BitWidth;
                return number2;
            }
            else
            {
                index = apostropheIndex + 1;
                if (index >= word.Length) return null;
                if( word.GetCharAt(index) == 's' || word.GetCharAt(index) == 'S')
                {
                    number.Signed = true;
                    index++;
                    if (index >= word.Length)
                    {
                        word.AddError("illegal number value");
                        return null;
                    }
                }
                if (sb.Length != 0)
                {
                    int bitWidth;
                    if (int.TryParse(sb.ToString(), out bitWidth))
                    {
                        number.BitWidth = bitWidth;
                    }
                }
                sb.Clear();
                switch(word.GetCharAt(index))
                {
                    case 'd':
                    case 'D':
                        if (!parseDecimalValue(number, word, ref index, sb)) return null;
                        break;
                    case 'b':
                    case 'B':
                        if (!parseBinaryValue(number, word, ref index, sb)) return null;
                        break;
                    case 'o':
                    case 'O':
                        if (!parseOctalValue(number, word, ref index, sb)) return null;
                        break;
                    case 'h':
                    case 'H':
                        if (!parseHexValue(number, word, ref index, sb)) return null;
                        break;
                    case '1':
                    case '0':
                    case 'x':
                    case 'z':
                        if (word.Length == 2) return parseSingleBitPadding(word, index);
                        return null;
                    default:
                        return null;
                }

            }
            word.MoveNext();
            return number;
        }

        private static Number parseSingleBitPadding(WordScanner word, int index)
        {
            Number number = new Number();
            switch (word.GetCharAt(index))
            {
                case '1':
                    number.Constant = true;
                    number.Value = 1;
                    number.NumberType = NumberTypeEnum.Decimal;
                    word.AddSystemVerilogError();
                    word.MoveNext();
                    return number;
                case '0':
                    number.Constant = true;
                    number.Value = 0;
                    number.NumberType = NumberTypeEnum.Decimal;
                    word.AddSystemVerilogError();
                    word.MoveNext();
                    return number;
                case 'x':
                    word.AddSystemVerilogError();
                    word.MoveNext();
                    return number;
                case 'z':
                    word.AddSystemVerilogError();
                    word.MoveNext();
                    return number;
            }
            return null;
        }


        private static bool parseRealValueAfterInteger(Number number, WordScanner word, ref int index, StringBuilder sb)
        {
            if (word.GetCharAt(index) == '.')
            { // real
                sb.Append('.');
                index++;
                if (index >= word.Length) return false;

                while (index < word.Length)
                {
                    if (word.GetCharAt(index) == 'e' || word.GetCharAt(index) == 'E') break;
                    if (word.GetCharAt(index) == '_')
                    {
                        index++;
                        continue;
//                        break;
                    }
                    if (!isDecimalDigit(word.GetCharAt(index))) return false;
                    sb.Append(word.GetCharAt(index));
                    index++;
                }
            }
            if (word.GetCharAt(index) == 'e' || word.GetCharAt(index) == 'E')
            { // real
                sb.Append('e');
                index++;
                if (index >= word.Length) return false;

                if (word.GetCharAt(index) == '+' || word.GetCharAt(index) == '-')
                {
                    sb.Append(word.GetCharAt(index));
                    index++;
                    if (index >= word.Length) return false;
                }

                if (!isDecimalDigit(word.GetCharAt(index))) return false;
                sb.Append(word.GetCharAt(index));
                index++;
                while (index < word.Length)
                {
                    if (word.GetCharAt(index) == '_')
                    {
                        index++;
                        continue;
//                        break;
                    }
                    if (!isDecimalDigit(word.GetCharAt(index))) return false;
                    sb.Append(word.GetCharAt(index));
                    index++;
                }
            }

            double value;
            if(double.TryParse(sb.ToString(),out value))
            {
                number.Value = value;
                number.Constant = true;
            }
            return true;
        }

        private static bool parseDecimalValue(Number number, WordScanner word, ref int index, StringBuilder sb)
        {
            sb.Clear();
            number.NumberType = NumberTypeEnum.Decimal;
            index++;
            if (index >= word.Length)
            {
                word.MoveNext();
                word.Color(CodeDrawStyle.ColorType.Number);
                index = 0;
                number.Text = number.Text + word.Text;
            }
            if (
                word.GetCharAt(index) == 'x'
                || word.GetCharAt(index) == 'X'
                || word.GetCharAt(index) == 'z'
                || word.GetCharAt(index) == 'Z'
                )
            {
                index++;
                while (index < word.Length)
                {
                    if (word.GetCharAt(index) != '_') return false;
                    index++;
                }
                return true;
            }

            while (index < word.Length)
            {
                if (!isDecimalDigit(word.GetCharAt(index))) return false;
                sb.Append(word.GetCharAt(index));
                index++;
            }
            int value;
            if(int.TryParse(sb.ToString(), out value))
            {
                number.Value = value;
                number.Constant = true;
            }
            return true;
        }

        private static bool parseBinaryValue(Number number, WordScanner word, ref int index, StringBuilder sb)
        {
            sb.Clear();
            number.NumberType = NumberTypeEnum.Binary;
            index++;
            if (index >= word.Length)
            {
                word.MoveNext();
                index = 0;
                word.Color(CodeDrawStyle.ColorType.Number);
                number.Text = number.Text + word.Text;
            }

            bool valueHeadError = false;
            while(index<word.Length && word.GetCharAt(index) == '_')
            {
                valueHeadError = true;
                index++;
            }
            if (valueHeadError)
            {
                word.AddWarning("hex_value can't start with _");
            }

            if (!isBinaryDigit(word.GetCharAt(index)))
            {
                word.AddError("illegal binary digit");
                return false;
            }
            sb.Append(word.GetCharAt(index));
            index++;
            while (index < word.Length)
            {
                if (word.GetCharAt(index) != '_')
                {
                    if (!isBinaryDigit(word.GetCharAt(index)))
                    {
                        word.AddError("illegal binary digit");
                        return false;
                    }
                    sb.Append(word.GetCharAt(index));
                }
                index++;
            }

            string valueString = sb.ToString();
            if (valueString.Contains('x') || valueString.Contains('?') || valueString.Contains('z'))
            {

            }
            else
            {
                try
                {
                    number.Value = Convert.ToInt32(sb.ToString(), 2);
                    number.Constant = true;
                }
                catch
                {

                }
            }

            return true;
        }

        private static bool parseOctalValue(Number number, WordScanner word, ref int index, StringBuilder sb)
        {
            sb.Clear();
            number.NumberType = NumberTypeEnum.Octal;
            index++;
            if (index >= word.Length)
            {
                word.MoveNext();
                index = 0;
                word.Color(CodeDrawStyle.ColorType.Number);
                number.Text = number.Text + word.Text;
            }
            if (!isOctalDigit(word.GetCharAt(index)))
            {
                word.AddError("iilegal octal digit");
                return false;
            }
            sb.Append(word.GetCharAt(index));
            index++;
            while (index < word.Length)
            {
                if (word.GetCharAt(index) != '_')
                {
                    if (!isOctalDigit(word.GetCharAt(index)))
                    {
                        word.AddError("iilegal octal digit");
                        return false;
                    }
                    sb.Append(word.GetCharAt(index));
                }
                index++;
            }
            try
            {
                number.Value = Convert.ToInt32(sb.ToString(), 8);
                number.Constant = true;
            }
            catch
            {

            }
            return true;
        }

        private static bool parseHexValue(Number number, WordScanner word, ref int index, StringBuilder sb)
        {
            sb.Clear();
            number.NumberType = NumberTypeEnum.Hex;
            index++;
            if (index >= word.Length)
            {
                word.MoveNext();
                index = 0;
                word.Color(CodeDrawStyle.ColorType.Number);
                number.Text = number.Text + word.Text;
            }

            bool hexHeadError = false;
            while (index < word.Length && word.GetCharAt(index) == '_')
            {
                hexHeadError = true;
                index++;
            }
            if(hexHeadError) word.AddError("_ not allowed at head of hex value");

            if (!isHexDigit(word.GetCharAt(index)))
            {
                word.AddError("iilegal hex digit");
                return false;
            }
            sb.Append(word.GetCharAt(index));
            index++;
            while (index < word.Length)
            {
                if (word.GetCharAt(index) != '_')
                {
                    if (!isHexDigit(word.GetCharAt(index)))
                    {
                        word.AddError("iilegal hex digit");
                        return false;
                    }
                    sb.Append(word.GetCharAt(index));
                }
                index++;
            }
            if(sb.Length <= 10)
            {
                int value;
                if(int.TryParse(sb.ToString(),System.Globalization.NumberStyles.HexNumber, null, out value))
                {
                    number.Value = value;
                    number.Constant = true;
                }
            }
            else
            {
                // overflow value
            }
            return true;
        }


        // decimal_digit ::= 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9  
        // binary_digit ::= x_digit | z_digit | 0 | 1  
        // octal_digit ::= x_digit | z_digit | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7  
        // hex_digit ::=  x_digit | z_digit | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | a | b | c | d | e | f | A | B | C | D | E | F  
        // x_digit ::= x | X 
        // z_digit ::= z | Z | ?

        private static bool isDecimalDigit(char value)
        {
            if (value >= '0' && value <= '9') return true;
            return false;
        }

        private static bool isBinaryDigit(char value)
        {
            if (value >= '0' && value <= '1') return true;
            if (value == 'x' || value == 'X') return true;                  // x_digit
            if (value == 'z' || value == 'Z' || value == '?') return true;  // z_digit
            return false;
        }

        private static bool isOctalDigit(char value)
        {
            if (value >= '0' && value <= '7') return true;
            if (value == 'x' || value == 'X') return true;                  // x_digit
            if (value == 'z' || value == 'Z' || value == '?') return true;  // z_digit
            return false;
        }

        private static bool isHexDigit(char value)
        {
            if (value >= '0' && value <= '9') return true;
            if (value >= 'a' && value <= 'f') return true;
            if (value >= 'A' && value <= 'F') return true;
            if (value == 'x' || value == 'X') return true;                  // x_digit
            if (value == 'z' || value == 'Z' || value == '?') return true;  // z_digit
            return false;
        }

        public static byte[] identifierTable = new byte[128] {
            //      0,1,2,3,4,5,6,7,8,9,a,b,c,e,d,f
            // 0*
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 1*
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 2*     ! " # $ % & ' ( ) * + , - . /
                    0,0,0,0,3,0,0,0,0,0,0,0,0,0,0,0,
            // 3*   0 1 2 3 4 5 6 7 8 9 : ; < = > ?
                    2,1,1,1,1,1,1,1,1,1,0,0,0,0,0,0,
            // 4*   @ A B C D E F G H I J K L M N O
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 5*   P Q R S T U V W X Y Z [ \ ] ^ _
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 6*   ` a b c d e f g h i j k l m n o
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 7*   p q r s t u v w x y z { | } ~ 
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0
        };

    }

}
