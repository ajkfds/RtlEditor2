using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    /// <summary>
    /// Document Tokenizer
    /// </summary>
    public class WordPointer
    {
        public WordPointer(CodeEditor.CodeDocument document, Verilog.ParsedDocument parsedDocument)
        {
            this.Document = document;
            this.ParsedDocument = parsedDocument;
            if (this.ParsedDocument == null) System.Diagnostics.Debugger.Break();
            string sectionName = SectionName;
            fetchNext(this.Document,ref index, out length, out nextIndex, out wordType, ref sectionName,true);
            SectionName = sectionName;
        }

        public void Dispose()
        {
            this.Document = null;
            this.ParsedDocument = null;
        }

        public CodeEditor.CodeDocument Document { get; protected set; }
        public Verilog.ParsedDocument ParsedDocument { get; protected set; }
        public bool InitibitColor = false;

        public Data.IVerilogRelatedFile VerilogFile
        {
            get
            {
                if (Document == null) return null;
                return Document.VerilogFile;
            }
        }

        public string SectionName { get; protected set; }

        protected int index = 0;
        protected int length = 0;
        protected int nextIndex;
        protected bool commentSkipped = false;
        protected int commentIndex = -1;

        protected int indexPrev = 0;
        protected bool commentSkippedPrev = false;
        protected int commentIndexPrev = -1;

        protected WordTypeEnum wordType = WordTypeEnum.Eof;

        public enum WordTypeEnum
        {
            Number,
            Symbol,
            Text,
            Comment,
            String,
            CompilerDirective,
            Eof
        }

        public WordPointer Clone()
        {
            WordPointer ret = new WordPointer(Document, ParsedDocument);
            ret.index = index;
            ret.length = length;
            ret.nextIndex = nextIndex;
            ret.wordType = wordType;
            return ret;
        }

        public void Color(CodeDrawStyle.ColorType colorType)
        {
            if (InitibitColor) return;
            for (int i = index; i < index + length; i++)
            {
                Document.SetColorAt(i, CodeDrawStyle.ColorIndex(colorType));
            }
        }

        public void Color(CodeDrawStyle.ColorType colorType,int start,int last)
        {
            if (InitibitColor) return;
//            if (last > length) System.Diagnostics.Debugger.Break();

            for (int i = index+start; i < index + last; i++)
            {
                Document.SetColorAt(i, CodeDrawStyle.ColorIndex(colorType));
            }
        }

        public void AppendBlock(int startIndex,int lastIndex)
        {
            Document.AppendBlock(startIndex, lastIndex);
        }

        /// <summary>
        /// Char Position Index in the Document
        /// </summary>
        public int Index
        {
            get
            {
                return index;
            }
        }

        /// <summary>
        /// Add error Message to the document
        /// </summary>
        /// <param name="message"></param>
        public void AddError(string message)
        {
            addError(index, length, message);
            if (ParsedDocument == null) return;
        }

        /// <summary>
        /// Add error message to the document reference
        /// </summary>
        /// <param name="fromReference"></param>
        /// <param name="message"></param>
        public void AddError(WordReference fromReference, string message)
        {
            addError(fromReference.Index, fromReference.Length, message);
        }

        private void addError(int index,int length, string message)
        {
            if (ParsedDocument == null) return;

            if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).ErrorCount < 100)
            {
                int lineNo = Document.GetLineAt(index);
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, message, Verilog.ParsedDocument.Message.MessageType.Error, index, lineNo, length, ParsedDocument.Project));
            }
            else if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).ErrorCount == 100)
            {
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, ">100 errors", Verilog.ParsedDocument.Message.MessageType.Error, 0, 0, 0, ParsedDocument.Project));
            }

            Document.SetMarkAt(index, length, 0);
            if (ParsedDocument is Verilog.ParsedDocument) (ParsedDocument as Verilog.ParsedDocument).ErrorCount++;
        }

        public void AddWarning(string message)
        {
            addWarning(index, length, message);
        }

        public void AddWarning(WordReference fromReference, string message)
        {
            addWarning(fromReference.Index, fromReference.Length, message);
        }
        private void addWarning(int index, int length, string message)
        {
            if (ParsedDocument == null) return;

            if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount < 100)
            {
                int lineNo = Document.GetLineAt(index);
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, message, Verilog.ParsedDocument.Message.MessageType.Warning, index, lineNo, length, ParsedDocument.Project));
            }
            else if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount == 100)
            {
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, ">100 warnings", Verilog.ParsedDocument.Message.MessageType.Warning, 0, 0, 0, ParsedDocument.Project));
            }

            Document.SetMarkAt(index, length, 1);
            if (ParsedDocument is Verilog.ParsedDocument) (ParsedDocument as Verilog.ParsedDocument).WarningCount++;
        }

        public void AddNotice(string message)
        {
            addNotice(index, length, message);
        }

        public void AddNotice(WordReference fromReference, string message)
        {
            addNotice(fromReference.Index, fromReference.Length, message);
        }

        private void addNotice(int index, int length, string message)
        {
            if (ParsedDocument == null) return;

            if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount < 100)
            {
                int lineNo = Document.GetLineAt(index);
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, message, Verilog.ParsedDocument.Message.MessageType.Notice, index, lineNo, length, ParsedDocument.Project));
            }
            else if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount == 100)
            {
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, ">100 notices", Verilog.ParsedDocument.Message.MessageType.Notice, 0, 0, 0, ParsedDocument.Project));
            }

            Document.SetMarkAt(index, length, 2);
            if (ParsedDocument is Verilog.ParsedDocument) (ParsedDocument as Verilog.ParsedDocument).WarningCount++;
        }

        public void AddHint(string message)
        {
            addHint(index, length, message);
        }

        public void AddHint(WordReference fromReference, string message)
        {
            addHint(fromReference.Index, fromReference.Length, message);
        }

        private void addHint(int index, int length, string message)
        {
            if (ParsedDocument == null) return;

            if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount < 100)
            {
                int lineNo = Document.GetLineAt(index);
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, message, Verilog.ParsedDocument.Message.MessageType.Hint, index, lineNo, length, ParsedDocument.Project));
            }
            else if (ParsedDocument is Verilog.ParsedDocument && (ParsedDocument as Verilog.ParsedDocument).WarningCount == 100)
            {
                ParsedDocument.Messages.Add(new Verilog.ParsedDocument.Message(Document.TextFile as Data.IVerilogRelatedFile, ">100 notices", Verilog.ParsedDocument.Message.MessageType.Hint, 0, 0, 0, ParsedDocument.Project));
            }

            Document.SetMarkAt(index, length, 3);
            if (ParsedDocument is Verilog.ParsedDocument) (ParsedDocument as Verilog.ParsedDocument).WarningCount++;
        }



        public void MoveNext()
        {
            indexPrev = index;
            index = nextIndex;
            if (Eof) return;

            string sectionName = SectionName;
            fetchNext(Document, ref index, out length, out nextIndex, out wordType,ref sectionName,true);
            SectionName = sectionName;
            commentSkippedPrev = commentSkipped;
            commentIndexPrev = commentIndex;

            commentSkipped = false;
            commentIndex = index;

            while (wordType == WordTypeEnum.Comment)
            {
                if (!Eof)
                {
                    commentSkipped = true;
                    index = nextIndex;
                    sectionName = SectionName;
                    fetchNext(Document, ref index, out length, out nextIndex, out wordType, ref sectionName, true);
                    SectionName = sectionName;
                }
                else
                {
                    commentSkipped = true;
                    index = nextIndex;
                    sectionName = SectionName;
                    break;
                }
            }
            if (!commentSkipped) commentIndex = -1;
        }

        public string GetPreviousComment()
        {
            if (!commentSkippedPrev) return "";
            return Document.CreateString(commentIndexPrev, indexPrev - commentIndexPrev);
        }

        public string GetFollowedComment()
        {
            if (!commentSkipped) return "";
            return Document.CreateString(commentIndex, index-commentIndex);
        }

        public CommentScanner GetCommentScanner()
        {
            return new CommentScanner(Document, commentIndex, index);
        }
        public void MoveNextUntilEol()
        {
            index = nextIndex;
            FetchNextUntilEol(Document, ref index, out length, out nextIndex, out wordType);
            commentSkipped = false;

            while (!Eof && wordType == WordTypeEnum.Comment)
            {
                commentSkipped = true;
                index = nextIndex;
                string sectionName = SectionName;
                fetchNext(Document, ref index, out length, out nextIndex, out wordType, ref sectionName, true);
                SectionName = sectionName;
            }
        }

        public bool Eof
        {
            get
            {
                if (nextIndex >= Document.Length)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public string Text
        {
            get
            {
                if (index + length > Document.Length) return "";
                return Document.CreateString(index, length);
            }
        }

        public WordTypeEnum WordType
        {
            get
            {
                return wordType;
            }
        }

        public int Length
        {
            get
            {
                return length;
            }
        }

        public char GetCharAt(int wordIndex)
        {
            return Document.GetCharAt(index + wordIndex);
        }

        public static void FetchNext(
            ajkControls.CodeTextbox.Document document,
            ref int index, out int length, out int nextIndex,
            out WordTypeEnum wordType,
            ref string sectionName
            )
        {
            fetchNext(
                document, 
                ref index, out length, out nextIndex, 
                out wordType, ref sectionName,
                false
                );
        }

        /// <summary>
        /// fetch next word token and updete properties
        /// </summary>
        /// <param name="document"></param>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <param name="nextIndex"></param>
        /// <param name="wordType"></param>
        /// <param name="sectionName"></param>
        /// <param name="colorComment"></param>
        private static void fetchNext(
            ajkControls.CodeTextbox.Document document,
            ref int index, out int length, out int nextIndex, 
            out WordTypeEnum wordType,
            ref string sectionName,
            bool colorComment
            )
        {
            // skip blanks before word

            char ch;
            int docLength = document.Length;
            unsafe
            {

                // skip blanks before word
                while(docLength > index)
                {
                    ch = document.GetCharAt(index);
                    if(ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r')
                    {
                        index++;
                        continue;
                    }
                    if ((ch & 0xff80) != 0) break;
                    if (!charClass0[ch]) break;
                    index++;
                }
                if(index == docLength)
                {
                    length = 0;
                    nextIndex = index;
                    wordType = WordTypeEnum.Eof;
                    return;
                }
            }

            length = 0;
            nextIndex = index;
            ch = document.GetCharAt(nextIndex);
            if (ch < 128)
            {
                switch (charClass[ch])
                {
                    case 1: // number
                    case 7: // quote
                        wordType = WordTypeEnum.Number;
                        fetchNextAtNumber(document, ref nextIndex, ref wordType);
                        break;
                    case 2: // alphabet
                        wordType = WordTypeEnum.Text;
                        nextIndex++;
                        while (docLength > nextIndex)
                        {
                            ch = document.GetCharAt(nextIndex);
                            if ((ch & 0xff80) == 0 && charClass1246[ch]) { nextIndex++; continue; }
                            break;
                        }
                        break;
                    case 6: // backquote (compiler directive)
                        wordType = WordTypeEnum.CompilerDirective;
                        nextIndex++;
                        while (docLength > nextIndex)
                        {
                            ch = document.GetCharAt(nextIndex);
                            if ((ch & 0xff80) == 0 && charClass1246[ch]) { nextIndex++; continue; }
                            break;
                        }
                        break;
                    case 3: // operators
                        wordType = WordTypeEnum.Symbol;
                        fetchNextAtOperator(document, ref nextIndex, ref wordType,ref sectionName, colorComment);
                        break;
                    case 4: // double quote
                        wordType = WordTypeEnum.String;
                        nextIndex++;
                        while (
                            docLength > nextIndex
                        )
                        {
                            if(document.GetCharAt(nextIndex) == '\"')
                            {
                                if(document.Length > nextIndex) nextIndex++;
                                break;
                            }
                            nextIndex++;
                        }
                        
                        break;
                    case 5: // blackslash
                        wordType = WordTypeEnum.Text;
                        nextIndex++;
                        while (docLength > nextIndex)
                        {
                            ch = document.GetCharAt(nextIndex);
                            if (ch == '\n') break;
                            if (ch == '\r') break;
                            if (ch == ' ') break;
                            if (ch == '\t') break;
                            nextIndex++;
                        }
                        break;
                    default:
                        wordType = WordTypeEnum.Text;
                        break;
                }
            }
            else
            {
                wordType = WordTypeEnum.Text;
                while (document.Length > nextIndex && document.GetCharAt(nextIndex) >= 128)
                {
                    nextIndex++;
                }
            }
            length = nextIndex - index;

        }

        public static void FetchNextUntilEol(
            ajkControls.CodeTextbox.Document document,
            ref int index, out int length, out int nextIndex,
            out WordTypeEnum wordType)
        {
            int docLength = document.Length;
            char ch;
            // skip blanks before word

            while (docLength > index)
            {
                ch = document.GetCharAt(index);
                if (ch == '\t') { index++; continue; }
                if (ch == ' ') { index++; continue; }
                break;
            }

            if (index == document.Length - 1)
            {
                length = 0;
                nextIndex = index;
                wordType = WordTypeEnum.Eof;
                return;
            }

            length = 0;
            nextIndex = index;

            wordType = WordTypeEnum.Text;
            nextIndex++;

            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch == '\r') break;
                if (ch == '\n') break;
                nextIndex++;
            }

            length = nextIndex - index;

            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch != '\r' && ch != '\n') break;
                nextIndex++;
            }
        }

        private static void fetchNextAtNumber(
            ajkControls.CodeTextbox.Document document,
            ref int nextIndex, ref WordTypeEnum wordType
            )
        {
            int docLength = document.Length;
            char ch;

            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if ((ch & 0xff80) != 0) break;
                if (charClass[ch] == 1) { nextIndex++; continue; }
                if (ch == '_') { nextIndex++; continue; }
                break;
            }

            if (docLength <= nextIndex) return;

            ch = document.GetCharAt(nextIndex);
            if (ch == '.' | ch == 'e' || ch == 'E')
            { // real
                if (ch == '.')
                {
                    nextIndex++;
                    if (document.Length <= nextIndex) return;

                    while (docLength > nextIndex)
                    {
                        ch = document.GetCharAt(nextIndex);
                        if ((ch & 0xff80) != 0) break;
                        if (charClass[ch] == 1) { nextIndex++; continue; }
                        if (ch == '_') { nextIndex++; continue; }
                        break;
                    }
                }

                ch = document.GetCharAt(nextIndex);
                if (ch == 'e' || ch == 'E')
                {
                    nextIndex++;
                    if (document.Length <= nextIndex) return;

                    ch = document.GetCharAt(nextIndex);
                    if (ch == '+' || ch == '-')
                    {
                        nextIndex++;
                        if (document.Length <= nextIndex) return;
                    }
                    while (docLength > nextIndex)
                    {
                        ch = document.GetCharAt(nextIndex);
                        if ((ch & 0xff80) != 0) break;
                        if (charClass[ch] == 1) { nextIndex++; continue; }
                        if (ch == '_') { nextIndex++; continue; }
                        break;
                    }
                }
            }
            else if (ch == '\'')
            {
                nextIndex++;
                if (docLength <= nextIndex) return;

                ch = document.GetCharAt(nextIndex);
                while (docLength > nextIndex)
                {
                    ch = document.GetCharAt(nextIndex);
                    if ((ch & 0xff80) != 0) break;
                    if (charClass[ch] == 1) { nextIndex++; continue; } // 0-9
                    if (charClass[ch] == 2) { nextIndex++; continue; } // alphabet
                    if (ch == '?') { nextIndex++; continue; }
                    if (ch == 'x') { nextIndex++; continue; }
                    if (ch == 'X') { nextIndex++; continue; }
                    if (ch == 'z') { nextIndex++; continue; }
                    if (ch == 'Z') { nextIndex++; continue; }
                    break;
                }
            }
            else
            {
                while (docLength > nextIndex)
                {
                    ch = document.GetCharAt(nextIndex);
                    if ((ch & 0xff80) != 0) break;
                    if (charClass[ch] == 1) { nextIndex++; continue; } // 0-9
                    if (charClass[ch] == 2) { nextIndex++; continue; } // alphabet
                    if (ch == '?') { nextIndex++; continue; }
                    if (ch == 'x') { nextIndex++; continue; }
                    if (ch == 'X') { nextIndex++; continue; }
                    if (ch == 'z') { nextIndex++; continue; }
                    if (ch == 'Z') { nextIndex++; continue; }
                    break;
                }
            }
        }

        private static void fetchNextAtOperator(
            ajkControls.CodeTextbox.Document document,
            ref int nextIndex,
            ref WordTypeEnum wordType,
            ref string sectionName,
            bool colorComment
        )
        {
            int docLength = document.Length;

            // comment block "/*...*/"
            if (docLength > nextIndex + 1 && document.GetCharAt(nextIndex) == '/' && document.GetCharAt(nextIndex + 1) == '*') // "/*"
            { // comment block
                if(colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                nextIndex++;
                if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                nextIndex++;
                while (docLength > nextIndex)
                {
                    if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                    if (document.GetCharAt(nextIndex) == '/' && document.GetCharAt(nextIndex - 1) == '*')
                    {
                        nextIndex++;
                        break;
                    }
                    nextIndex++;
                }
                wordType = WordTypeEnum.Comment;
                return;
            }

            // line comment "// ..."
            if (docLength > nextIndex + 1 && document.GetCharAt(nextIndex) == '/' && document.GetCharAt(nextIndex + 1) == '/') // "//"
            { // line comment
                if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                nextIndex++;
                if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                nextIndex++;
                while (docLength > nextIndex && document.GetCharAt(nextIndex) != '\n')
                {
                    if(colorComment && document.GetCharAt(nextIndex) == '@')
                    {
                        ParseInLineComments(document, ref nextIndex, ref wordType,ref sectionName);
                        continue;
                    }
                    if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                    nextIndex++;
                }
                if (docLength > nextIndex && document.GetCharAt(nextIndex) == '\n')
                {
                    if (colorComment) document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                    nextIndex++;
                }
                wordType = WordTypeEnum.Comment;
                return;
            }

            string op4 = "";
            if (docLength > nextIndex + 3)
            {
                op4 = document.CreateString(nextIndex, 4);
            }

            string op3 = "";
            if (docLength > nextIndex + 2)
            {
                op3 = document.CreateString(nextIndex, 3);
            }

            string op2 = "";
            if (docLength > nextIndex + 1)
            {
                op2 = document.CreateString(nextIndex, 2);
            }


            if (
                    op4 == "<<<=" ||
                    op4 == ">>>="
            )
            {
                nextIndex = nextIndex + 4;
                return;
            }

            if (
                    op3 == ">>>" ||
                    op3 == "<<<" ||
                    op3 == "===" ||
                    op3 == "!==" ||
                    op3 == "<<=" ||
                    op3 == ">>=" ||
                    op3 == "==?" ||
                    op3 == "!=?" ||
                    op3 == "<->"
            )
            {
                nextIndex = nextIndex + 3;
                return;
            }

            if (
                op2 == "==" ||
                op2 == "!=" ||
                op2 == "&&" ||
                op2 == "||" ||
                op2 == ">>" ||
                op2 == "<<" ||
                op2 == "<=" ||
                op2 == ">=" ||
                op2 == "~|" ||
                op2 == "~&" ||
                op2 == "~^" ||
                op2 == "^~" ||
                op2 == "**" ||
                op2 == "(*" ||
                op2 == "*)" ||
                op2 == "+:" ||
                op2 == "-:" ||
                op2 == "=>" ||
                op2 == "+=" ||
                op2 == "-=" ||
                op2 == "*=" ||
                op2 == "/=" ||
                op2 == "%=" ||
                op2 == "&=" ||
                op2 == "|=" ||
                op2 == "^=" ||
                op2 == "->" ||
                op2 == "++" ||
                op2 == "--"
            )
            {
                nextIndex = nextIndex + 2;
                return;
            }

            if (op2 == "->")
            {
                wordType = WordTypeEnum.Text;
                nextIndex = nextIndex + 2;
                return;
            }

            nextIndex++;
        }

        private static void ParseInLineComments(
            ajkControls.CodeTextbox.Document document, 
            ref int nextIndex, 
            ref WordTypeEnum wordType,
            ref string sectionName
            )
        {
            int docLength = document.Length;
            char ch;
            if (docLength <= nextIndex || document.GetCharAt(nextIndex) == '\n') return;

            string target = "@section";
            int i = 0;

            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch == '\n') return;
                if (ch != target[i]) return;
                document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.Comment));
                nextIndex++;
                i++;

                if (i >= target.Length) break;
            }

            for (int j = nextIndex - i; j < nextIndex; j++)
            {
                document.SetColorAt(j, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.HighLightedComment));
            }
            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch != ' ' && ch != '\t') break;
                nextIndex++;
            }
            StringBuilder sb = new StringBuilder();
            while (docLength > nextIndex)
            {
                ch = document.GetCharAt(nextIndex);
                if (ch == '\n' || ch == '\r') break;
                document.SetColorAt(nextIndex, CodeDrawStyle.ColorIndex(CodeDrawStyle.ColorType.HighLightedComment));
                sb.Append(ch);
                nextIndex++;
            }
            sectionName = sb.ToString();

        }

        private static byte[] charClass = new byte[128]
        {
            //      0,1,2,3,4,5,6,7,8,9,a,b,c,e,d,f
            // 0*
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 1*
                    0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            // 2*     ! " # $ % & ' ( ) * + , - . /
                    0,3,4,3,2,3,3,7,3,3,3,3,3,3,3,3,
            // 3*   0 1 2 3 4 5 6 7 8 9 : ; < = > ?
                    1,1,1,1,1,1,1,1,1,1,3,3,3,3,3,3,
            // 4*   @ A B C D E F G H I J K L M N O
                    3,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,
            // 5*   P Q R S T U V W X Y Z [ \ ] ^ _
                    2,2,2,2,2,2,2,2,2,2,2,3,5,3,3,2,
            // 6*   ` a b c d e f g h i j k l m n o
                    6,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,
            // 7*   p q r s t u v w x y z { | } ~ 
                    2,2,2,2,2,2,2,2,2,2,2,3,3,3,3,0
        };

        private static bool[] charClass0 = new bool[128]
        {
            //      0,1,2,3,4,5,6,7,8,9,a,b,c,e,d,f
            // 0*
                    true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,
            // 1*
                    true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,
            // 2*     ! " # $ % & ' ( ) * + , - . /
                    true,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 3*   0 1 2 3 4 5 6 7 8 9 : ; < = > ?
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 4*   @ A B C D E F G H I J K L M N O
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 5*   P Q R S T U V W X Y Z [ \ ] ^ _
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 6*   ` a b c d e f g h i j k l m n o
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 7*   p q r s t u v w x y z { | } ~ 
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,true
        };

        private static bool[] charClass1246 = new bool[128]
        {
            //      0,1,2,3,4,5,6,7,8,9,a,b,c,e,d,f
            // 0*
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 1*
                    false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,
            // 2*     ! " # $ % & ' ( ) * + , - . /
                    false,false,true,false,true,false,false,false,false,false,false,false,false,false,false,false,
            // 3*   0 1 2 3 4 5 6 7 8 9 : ; < = > ?
                    true,true,true,true,true,true,true,true,true,true,false,false,false,false,false,false,
            // 4*   @ A B C D E F G H I J K L M N O
                    false,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,
            // 5*   P Q R S T U V W X Y Z [ \ ] ^ _
                    true,true,true,true,true,true,true,true,true,true,true,false,false,false,false,true,
            // 6*   ` a b c d e f g h i j k l m n o
                    true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,
            // 7*   p q r s t u v w x y z { | } ~ 
                    true,true,true,true,true,true,true,true,true,true,true,false,false,false,false,false
        };

    }
}

