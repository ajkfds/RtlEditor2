using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using ajkControls.Primitive;

namespace pluginVerilog.Verilog.AutoComplete
{
    public class GenerateAutoCompleteItem : codeEditor.CodeEditor.AutocompleteItem
    {
        public GenerateAutoCompleteItem(string text, byte colorIndex, Color color) : base(text, colorIndex, color)
        {
        }
        public GenerateAutoCompleteItem(string text, byte colorIndex, Color color, IconImage icon, IconImage.ColorStyle iconColorStyle) : base(text, colorIndex, color, icon, iconColorStyle)
        {
        }

        public override void Apply(codeEditor.CodeEditor.CodeDocument codeDocument, System.Windows.Forms.KeyEventArgs e)
        {
            int prevIndex = codeDocument.CaretIndex;
            if (codeDocument.GetLineStartIndex(codeDocument.GetLineAt(prevIndex)) != prevIndex && prevIndex != 0)
            {
                prevIndex--;
            }
            char currentChar = codeDocument.GetCharAt(codeDocument.CaretIndex);
            if (currentChar != '\r' && currentChar != '\n') return;
            string indent = (codeDocument as CodeEditor.CodeDocument).GetIndentString(prevIndex);

            int headIndex, length;
            codeDocument.GetWord(prevIndex, out headIndex, out length);
            codeDocument.Replace(headIndex, length, ColorIndex, Text + "\r\n"+indent+"endgenerate");
            codeDocument.CaretIndex = headIndex + Text.Length;
            codeDocument.SelectionStart = headIndex + Text.Length;
            codeDocument.SelectionLast = headIndex + Text.Length;
        }
    }
}
