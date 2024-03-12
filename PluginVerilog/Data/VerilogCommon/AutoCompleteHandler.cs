using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using codeEditor.Data;

namespace pluginVerilog.Data.VerilogCommon
{
    public static class AutoComplete
    {
        public static void AfterKeyDown(IVerilogRelatedFile item, System.Windows.Forms.KeyEventArgs e)
        {
            if (item.VerilogParsedDocument == null) return;
            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.Return:
                    applyAutoInput(item);
                    break;
                case System.Windows.Forms.Keys.Space:
                    break;
                default:
                    break;
            }
        }

        public static void AfterKeyPressed(IVerilogRelatedFile item, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (item.VerilogParsedDocument == null) return;
        }

        public static void BeforeKeyPressed(IVerilogRelatedFile item, System.Windows.Forms.KeyPressEventArgs e)
        {
        }

        public static void BeforeKeyDown(IVerilogRelatedFile item, System.Windows.Forms.KeyEventArgs e)
        {
        }


        public static List<codeEditor.CodeEditor.PopupItem> GetPopupItems(IVerilogRelatedFile item, Verilog.ParsedDocument parsedDocument, ulong version, int index)
        {
            if (parsedDocument == null) return null;
            if (parsedDocument.Version != version) return null;

            int headIndex, length;
            item.CodeDocument.GetWord(index, out headIndex, out length);
            string text = item.CodeDocument.CreateString(headIndex, length);
            return parsedDocument.GetPopupItems(index, text);
        }


        public static List<codeEditor.CodeEditor.ToolItem> GetToolItems(IVerilogRelatedFile item, int index)
        {
            List<codeEditor.CodeEditor.ToolItem> toolItems = new List<codeEditor.CodeEditor.ToolItem>();
            toolItems.Add(new Verilog.Snippets.AlwaysFFSnippet());
            toolItems.Add(new Verilog.Snippets.AutoConnectSnippet());
            //            toolItems.Add(new Verilog.Snippets.ConnectionCheckSnippet());
            toolItems.Add(new Verilog.Snippets.AutoFormatSnippet());
            toolItems.Add(new Verilog.Snippets.ModuleInstanceSnippet());
            return toolItems;
        }

        public static List<codeEditor.CodeEditor.AutocompleteItem> GetAutoCompleteItems(IVerilogRelatedFile item,Verilog.ParsedDocument parsedDocument, int index, out string cantidateWord)
        {
            cantidateWord = null;

            if (item.VerilogParsedDocument == null) return null;
            int line = item.CodeDocument.GetLineAt(index);
            int lineStartIndex = item.CodeDocument.GetLineStartIndex(line);
            bool endWithDot;
            List<string> words = ((pluginVerilog.CodeEditor.CodeDocument)item.CodeDocument).GetHierWords(index, out endWithDot);
            if (endWithDot)
            {
                cantidateWord = "";
            }
            else
            {
                cantidateWord = words.LastOrDefault();
                if (words.Count > 0)
                {
                    words.RemoveAt(words.Count - 1);
                }
            }
            if (cantidateWord == null) cantidateWord = "";

            List<codeEditor.CodeEditor.AutocompleteItem> items = parsedDocument.GetAutoCompleteItems(words, lineStartIndex, line, (CodeEditor.CodeDocument)item.CodeDocument, cantidateWord);

            return items;
        }



        private static void applyAutoInput(IVerilogRelatedFile item)
        {
            int index = item.CodeDocument.CaretIndex;
            int line = item.CodeDocument.GetLineAt(index);
            if (line == 0) return;

            int lineHeadIndex = item.CodeDocument.GetLineStartIndex(line);

            int prevTabs = 0;
            if (line != 1)
            {
                int prevLine = line - 1;
                int prevLineHeadIndex = item.CodeDocument.GetLineStartIndex(prevLine);
                for (int i = prevLineHeadIndex; i < lineHeadIndex; i++)
                {
                    char ch = item.CodeDocument.GetCharAt(i);
                    if (ch == '\t')
                    {
                        prevTabs++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            int indentLength = 0;
            for (int i = lineHeadIndex; i < item.CodeDocument.Length; i++)
            {
                char ch = item.CodeDocument.GetCharAt(i);
                if (ch == '\t')
                {
                    indentLength++;
                }
                else if (ch == ' ')
                {
                    indentLength++;
                }
                else
                {
                    break;
                }
            }


            bool prevBegin = isPrevBegin(item,lineHeadIndex);
            bool nextEnd = isNextEnd(item, lineHeadIndex);

            if (prevBegin)
            {
                if (nextEnd) // caret is sandwiched beteen begin and end
                {
                    // BEFORE
                    // begin[enter] end

                    // AFTER
                    // begin
                    //     [caret]
                    // end
                    item.CodeDocument.Replace(lineHeadIndex, indentLength, 0, new String('\t', prevTabs + 1) + "\r\n" + new String('\t', prevTabs));
                    item.CodeDocument.CaretIndex = item.CodeDocument.CaretIndex + prevTabs + 1 + 1 - indentLength;
                    return;
                }
                else
                {   // add indent
                    prevTabs++;
                }
            }

            if (prevTabs != 0) item.CodeDocument.Replace(lineHeadIndex, indentLength, 0, new String('\t', prevTabs));
            item.CodeDocument.CaretIndex = item.CodeDocument.CaretIndex + prevTabs - indentLength;
        }

        private static bool isPrevBegin(IVerilogRelatedFile item, int index)
        {
            int prevInex = index;
            if (prevInex > 0) prevInex--;

            if (prevInex > 0 && item.CodeDocument.GetCharAt(prevInex) == '\n') prevInex--;
            if (prevInex > 0 && item.CodeDocument.GetCharAt(prevInex) == '\r') prevInex--;

            if (prevInex == 0 || item.CodeDocument.GetCharAt(prevInex) != 'n') return false;
            prevInex--;
            if (prevInex == 0 || item.CodeDocument.GetCharAt(prevInex) != 'i') return false;
            prevInex--;
            if (prevInex == 0 || item.CodeDocument.GetCharAt(prevInex) != 'g') return false;
            prevInex--;
            if (prevInex == 0 || item.CodeDocument.GetCharAt(prevInex) != 'e') return false;
            prevInex--;
            if (item.CodeDocument.GetCharAt(prevInex) != 'b') return false;
            return true;
        }

        private static bool isNextEnd(IVerilogRelatedFile item, int index)
        {
            int prevInex = index;
            if (prevInex < item.CodeDocument.Length &&
                (
                    item.CodeDocument.GetCharAt(prevInex) == ' ' || item.CodeDocument.GetCharAt(prevInex) == '\t'
                )
            ) prevInex++;

            if (prevInex >= item.CodeDocument.Length || item.CodeDocument.GetCharAt(prevInex) != 'e') return false;
            prevInex++;
            if (prevInex >= item.CodeDocument.Length || item.CodeDocument.GetCharAt(prevInex) != 'n') return false;
            prevInex++;
            if (item.CodeDocument.GetCharAt(prevInex) != 'd') return false;
            return true;
        }

    }
}
