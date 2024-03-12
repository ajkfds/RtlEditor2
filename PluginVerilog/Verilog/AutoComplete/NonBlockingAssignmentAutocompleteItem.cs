using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using System.Drawing;

namespace pluginVerilog.Verilog.AutoComplete
{
    public class NonBlockingAssignmentAutoCompleteItem : codeEditor.CodeEditor.AutocompleteItem
    {
        public NonBlockingAssignmentAutoCompleteItem(string text, byte colorIndex, Color color) : base(text, colorIndex, color)
        {
        }

        public override void Apply(codeEditor.CodeEditor.CodeDocument codeDocument, System.Windows.Forms.KeyEventArgs e)
        {
            int prevIndex = codeDocument.CaretIndex;
            if (codeDocument.GetLineStartIndex(codeDocument.GetLineAt(prevIndex)) != prevIndex && prevIndex != 0)
            {
                prevIndex--;
            }
            int headIndex, length;
            codeDocument.GetWord(prevIndex, out headIndex, out length);

            char currentChar = codeDocument.GetCharAt(codeDocument.CaretIndex);
            string appendText = " #P_DELAY";
            if (currentChar != '\r' && currentChar != '\n')
            {
                appendText = "";
            }

            codeDocument.Replace(headIndex, length, ColorIndex, Text + appendText );
            codeDocument.CaretIndex = headIndex + Text.Length + appendText.Length;
            codeDocument.SelectionStart = headIndex + Text.Length;
            codeDocument.SelectionLast = headIndex + Text.Length;
        }

    }
}
