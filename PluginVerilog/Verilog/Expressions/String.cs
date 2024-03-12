using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog.Expressions
{
    public class ConstantString : Primary
    {
        protected ConstantString() { }
        public static ConstantString ParseCreate(WordScanner word)
        {
            word.Color(CodeDrawStyle.ColorType.Number);
            ConstantString str = new ConstantString();
            str.text = word.Text;

            str.Reference = word.GetReference();
            str.BitWidth = (word.Text.Length - 2) * 8;
            str.Constant = true;
            if(str.BitWidth <= 16)
            {
                int value = 0;
                for(int i = 1;i< word.Text.Length-1; i++)
                {
                    value = value << 8;
                    value = value + word.Text[i];
                }
                str.Value = value;
            }

            word.MoveNext();
            return str;
        }

        protected string text;
        public string Text
        {
            get { return text; }
        }
        public override string CreateString()
        {
            return Text;
        }
        public override void AppendLabel(ajkControls.ColorLabel.ColorLabel label)
        {
            label.AppendText(Text, Global.CodeDrawStyle.Color(CodeDrawStyle.ColorType.Variable));
        }

        public override void AppendString(StringBuilder stringBuilder)
        {
            stringBuilder.Append(Text);
        }


    }
}
