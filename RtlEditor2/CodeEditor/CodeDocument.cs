using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Document;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.CodeEditor
{
    public class CodeDocument : IDisposable
    {
        public CodeDocument()
        {
            textDocument = new TextDocument();
            textDocument.SetOwnerThread(System.Threading.Thread.CurrentThread);
        }

        public CodeDocument(Data.TextFile textFile, bool textOnly) : this()
        {

        }
        public CodeDocument(Data.TextFile textFile) : this()
        {
            textFileRef = new WeakReference<Data.TextFile>(textFile);
        }

        public CodeDocument(Data.TextFile textFile, string text) : this()
        {
            textFileRef = new WeakReference<Data.TextFile>(textFile);
        }

        private TextDocument textDocument;
        public TextDocument TextDocument
        {
            get
            {
                return textDocument;
            }
        }


        private ITextSourceVersion snapShotVersion = null;
        private AvaloniaEdit.Document.ITextSource snapShot = null;
        private TextDocument snapedTextDocument = null;

        public AvaloniaEdit.Document.ITextSource TextDocumentSnapShot
        {
            get
            {
                if (textDocument == null) return null;
                if(textDocument.Version != snapShotVersion)
                {
                    snapShot = textDocument.CreateSnapshot();
                    snapShotVersion = snapShot.Version;
                    snapedTextDocument = new TextDocument(snapShot);
                }
                return snapShot;
            }
        }

        public System.WeakReference<Data.TextFile>? textFileRef;
        public Data.TextFile TextFile
        {
            get
            {
                Data.TextFile ret;
                if (textFileRef == null) return null;
                if (!textFileRef.TryGetTarget(out ret)) return null;
                return ret;
            }
        }

        private readonly bool textOnly = false;
        private bool disposed = false;
        public void Dispose()
        {
            //if (Global.mainForm.editorPage.CodeEditor.codeTextbox.Document == this)
            //{
            //    Global.mainForm.editorPage.CodeEditor.codeTextbox.Document = null;
            //}
            disposed = true;
            //chars.Dispose();
            if (!textOnly)
            {
                //colors.Dispose();
                //marks.Dispose();
                //newLineIndex.Dispose();
                //lineVisible.Dispose();
            }
        }

        public bool IsDisposed
        {
            get
            {
                return disposed;
            }
        }

        public void Clean()
        {
            CleanVersion = Version;
        }

        public bool IsDirty
        {
            get
            {
                if (CleanVersion == Version) return false;
                return true;
            }
        }

        public Action<int, int, byte, string>? Replaced = null;


        private int visibleLines = 0;
        List<int> collapsedLines = new List<int>();

        public virtual ulong Version { get; set; } = 0;

        public ulong CleanVersion { get; private set; } = 0;

        List<History> histories = new List<History>();
        public int HistoryMaxLimit = 100;

        public class History
        {
            public History(int index, int length, string changedFrom)
            {
                Index = index;
                Length = length;
                ChangedFrom = changedFrom;
            }
            public readonly int Index;
            public readonly int Length;
            public readonly string ChangedFrom;
        }

        public int Length
        {
            get
            {
                if (snapShot == null) return 0;
                return snapShot.TextLength;
            }
        }


        // block handling /////////////////////////////

        List<int> blockStartIndexs = new List<int>();
        List<int> blockEndIndexs = new List<int>();

        // block infomation cash
        bool blockCashActive = false;
        List<int> blockStartLines = new List<int>();
        List<int> blockEndLines = new List<int>();
        private void createBlockCash()
        {
            if (textOnly) return;

            blockStartLines.Clear();
            blockEndLines.Clear();
            for (int i = 0; i < blockStartIndexs.Count; i++)
            {
                blockStartLines.Add(GetLineAt(blockStartIndexs[i]));
                blockEndLines.Add(GetLineAt(blockEndIndexs[i]));
            }
            blockCashActive = true;
        }



        public int VisibleLines
        {
            get
            {
                return visibleLines;
            }
        }

        //public int GetVisibleLineNo(int lineNo)
        //{
        //    if (textOnly) return 0;

        //    if (!blockCashActive) createBlockCash();
        //    if (collapsedLines.Count == 0) return lineNo;
        //    int vline = 0;
        //    for (int i = 0; i < lineNo; i++)
        //    {
        //        if (lineVisible[i]) vline++;
        //    }
        //    return vline;
        //}

        //public int GetActialLineNo(int visibleLineNo)
        //{
        //    if (textOnly) return 0;

        //    if (!blockCashActive) createBlockCash();
        //    if (collapsedLines.Count == 0) return visibleLineNo;
        //    int lineNo = 0;
        //    int vLine = 0;
        //    for (lineNo = 0; lineNo < Lines; lineNo++)
        //    {
        //        if (lineVisible[lineNo]) vLine++;
        //        if (visibleLineNo == vLine) break;
        //    }
        //    if (lineNo == 0) lineNo = 1;
        //    return lineNo;
        //}

        public void ClearBlock()
        {
            blockCashActive = false;
            blockStartIndexs.Clear();
            blockEndIndexs.Clear();
        }
        public void AppendBlock(int startIndex, int endIndex)
        {
            blockCashActive = false;
            blockStartIndexs.Add(startIndex);
            blockEndIndexs.Add(endIndex);
        }

        //public bool IsVisibleLine(int lineNo)
        //{
        //    if (!blockCashActive) createBlockCash();
        //    return lineVisible[lineNo - 1];
        //}
        public bool IsBlockHeadLine(int lineNo)
        {
            if (!blockCashActive) createBlockCash();
            return blockStartLines.Contains(lineNo);
        }

        //public void CollapseBlock(int lineNo)
        //{
        //    if (!blockCashActive) createBlockCash();
        //    if (!blockStartLines.Contains(lineNo)) return;
        //    if (!collapsedLines.Contains(lineNo))
        //    {
        //        collapsedLines.Add(lineNo);
        //        refreshVisibleLines();
        //    }
        //}

        //public void ExpandBlock(int lineNo)
        //{
        //    if (!blockCashActive) createBlockCash();
        //    if (!blockStartLines.Contains(lineNo)) return;
        //    if (collapsedLines.Contains(lineNo))
        //    {
        //        collapsedLines.Remove(lineNo);
        //        refreshVisibleLines();
        //    }
        //}

        public bool IsCollapsed(int lineNo)
        {
            if (!blockStartLines.Contains(lineNo)) return false;
            if (collapsedLines.Contains(lineNo)) return true;
            return false;
        }

        /////////////////////////////////////////

        int selectionStart;
        public int SelectionStart
        {
            get
            {
                return selectionStart;
            }
            set
            {
                selectionStart = value;
            }
        }

        int selectionLast;
        public int SelectionLast
        {
            get
            {
                return selectionLast;
            }
            set
            {
                selectionLast = value;
            }
        }

        int caretIndex;
        public int CaretIndex
        {
            get
            {
                return caretIndex;
            }
            set
            {
                caretIndex = value;
            }
        }

        public char GetCharAt(int index)
        {
            return textDocument.GetCharAt(index);
        }

        public void SetCharAt(int index, char value)
        {
            textDocument.Replace(index, 1, value.ToString());
        }

        public void CopyColorMarkFrom(CodeDocument document)
        {
//            copyFrom(document, false, true, true);
        }
        public void CopyFrom(CodeDocument document)
        {
//            copyFrom(document, true, true, true);
        }
        public void CopyTextOnlyFrom(CodeDocument document)
        {
//            copyFrom(document, true, false, false);
            Version = document.Version;
        }

        public byte GetMarkAt(int index)
        {
            //            return marks[index];
            return 0;
        }

        public void SetMarkAt(int index, byte value)
        {
            if (index >= Length) return;
            //marks[index] |= (byte)(1 << value);
        }

        public void SetMarkAt(int index, int length, byte value)
        {
            for (int i = index; i < index + length; i++)
            {
                SetMarkAt(i, value);
            }
        }


        public void RemoveMarkAt(int index, byte value)
        {
//            marks[index] &= (byte)((1 << value) ^ 0xff);
        }
        public void RemoveMarkAt(int index, int length, byte value)
        {
            for (int i = index; i < index + length; i++)
            {
                RemoveMarkAt(i, value);
            }
        }

        public Dictionary<DocumentLine, LineInfomation> LineInfomations = new Dictionary<DocumentLine, LineInfomation>();

        public byte GetColorAt(int index)
        {
            //            return colors[index];
            return 0;
        }


        public void SetColorAt(int index, byte value)
        {
            if (textDocument == null) return;
            DocumentLine line = textDocument.GetLineByOffset(index);
            LineInfomation lineInfo;
            if (LineInfomations.ContainsKey(line))
            {
                lineInfo = LineInfomations[line];
            }
            else
            {
                lineInfo = new LineInfomation();
                LineInfomations.Add(line, lineInfo);
            }
            lineInfo.Colors.Add(new LineInfomation.Color(index, 1, value));
        }



        public void Undo()
        {
            //lock (this)
            //{
            //    if (histories.Count == 0) return;
            //    History history = histories.Last();
            //    histories.RemoveAt(histories.Count - 1);
            //    Version--;
            //    replace(history.Index, history.Length, 0, history.ChangedFrom);
            //}
        }

        public void ClearHistory()
        {
            histories.Clear();
        }

        public void Replace(int index, int replaceLength, byte colorIndex, string text)
        {
            if (textDocument == null) return;
            lock (this)
            {
                textDocument.Replace(index, replaceLength, text);
                // set color
            }
        }


        public int GetLineAt(int index)
        {
            if (textDocument == null) return 0;
            return textDocument.GetLineByOffset(index).LineNumber;
        }

        //public int GetVisibleLine(int line)
        //{
        //    lock (this)
        //    {
        //        int visibleLine = 0;
        //        for (int l = 0; l < line; l++)
        //        {
        //            if (lineVisible[l]) visibleLine++;
        //        }
        //        return visibleLine;
        //    }
        //}


        public int GetLineStartIndex(int line)
        {
            if (snapedTextDocument == null) return 0;
            TextLocation location = new TextLocation(line, 0);
            return snapedTextDocument.GetOffset(location);
        }

        public int GetLineLength(int line)
        {
            if (snapedTextDocument == null) return 0;
            return snapedTextDocument.GetLineByNumber(line).Length;
        }

        public int Lines
        {
            get
            {
                if (snapedTextDocument == null) return 0;
                return snapedTextDocument.LineCount;
            }
        }

        public int FindIndexOf(string targetString, int startIndex)
        {
            if (snapedTextDocument == null) return -1;
            if (targetString.Length == 0) return -1;
            for (int i = startIndex; i < Length - targetString.Length; i++)
            {
                if (targetString[0] != snapedTextDocument.GetCharAt(i)) continue;
                bool match = true;
                for (int j = 1; j < targetString.Length; j++)
                {
                    if (targetString[j] != snapedTextDocument.GetCharAt(i + j))
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        public int FindPreviousIndexOf(string targetString, int startIndex)
        {
            lock (this)
            {
                if (targetString.Length == 0) return -1;
                if (startIndex > Length - targetString.Length) startIndex = Length - targetString.Length;

                for (int i = startIndex; i >= 0; i--)
                {
                    if (targetString[0] != textDocument.GetCharAt(i)) continue;
                    bool match = true;
                    for (int j = 1; j < targetString.Length; j++)
                    {
                        if (targetString[j] != textDocument.GetCharAt(i + j))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return i;
                }
                return -1;
            }
        }

        public string CreateString()
        {
            return textDocument.GetText(0, textDocument.TextLength);
        }

        public string CreateString(int index, int length)
        {
            return textDocument.GetText(index, length);
        }

        //public char[] CreateCharArray()
        //{
        //    return chars.CreateArray();
        //}

        public string CreateLineString(int line)
        {
            return snapedTextDocument.GetText(GetLineStartIndex(line), GetLineLength(line));
        }

        //public char[] CreateLineArray(int line)
        //{
        //    unsafe
        //    {
        //        char[] array = chars.CreateArray(GetLineStartIndex(line), GetLineLength(line));
        //        return array;
        //    }
        //}

        public virtual void GetWord(int index, out int headIndex, out int length)
        {
            lock (this)
            {
                headIndex = index;
                length = 0;
                char ch = GetCharAt(index);
                if (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t') return;

                while (headIndex > 0)
                {
                    ch = GetCharAt(headIndex);
                    if (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t')
                    {
                        break;
                    }
                    headIndex--;
                }
                headIndex++;

                while (headIndex + length < Length)
                {
                    ch = GetCharAt(headIndex + length);
                    if (ch == ' ' || ch == '\r' || ch == '\n' || ch == '\t')
                    {
                        break;
                    }
                    length++;
                }
            }
        }


    }
}
