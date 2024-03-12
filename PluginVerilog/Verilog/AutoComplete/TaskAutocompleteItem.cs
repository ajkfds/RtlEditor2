using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace pluginVerilog.Verilog.AutoComplete
{
    public class TaskAutocompleteItem : codeEditor.CodeEditor.AutocompleteItem
    {
        public TaskAutocompleteItem(string text, byte colorIndex, Color color) : base(text, colorIndex, color)
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
//            string appendText = ";\r\nbegin\r\nend\r\nendtask";
            string appendText = "\r\nendtask";
            if (currentChar != '\r' && currentChar != '\n')
            {
                appendText = "";
            }

            codeDocument.Replace(headIndex, length, ColorIndex, Text + appendText);
            codeDocument.CaretIndex = headIndex + Text.Length;
            codeDocument.SelectionStart = headIndex + Text.Length;
            codeDocument.SelectionLast = headIndex + Text.Length;
        }
    }
}
