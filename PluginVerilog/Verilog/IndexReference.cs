using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class IndexReference
    {
        public static IndexReference Create(WordPointer wordPointer,List<WordPointer> stocks)
        {
            IndexReference ret = new IndexReference();
            if(stocks.Count == 0)
            {
                ret.RootParsedDocument = wordPointer.ParsedDocument;
                ret.Indexs.Add(wordPointer.Index);
            }
            else
            {
                ret.RootParsedDocument = stocks[0].ParsedDocument;
                ret.Indexs.Add(stocks[0].Index);
            }

            foreach(WordPointer wp in stocks)
            {
                ret.Indexs.Add(wp.Index);
            }
            return ret;
        }

        public static IndexReference Create(ParsedDocument rootParsedDocument)
        {
            IndexReference ret = new IndexReference();
            ret.RootParsedDocument = rootParsedDocument;
            return ret;
        }

        public static IndexReference Create(IndexReference indexReference, int index)
        {
            IndexReference ret = indexReference.Clone();
            ret.Indexs.Add(index);
            return ret;
        }

        public ParsedDocument GetNodeParsedDocument()
        {
            ParsedDocument pDocument = RootParsedDocument;
            foreach(int index in Indexs)
            {
                if (!pDocument.ParsedDocumentIndexDictionary.ContainsKey(index)) return null;
                pDocument = pDocument.ParsedDocumentIndexDictionary[index];
            }
            return pDocument;
        }


        public IndexReference Clone()
        {
            IndexReference ret = new IndexReference();
            ret.RootParsedDocument = RootParsedDocument;
            foreach (int index in Indexs)
            {
                ret.Indexs.Add(index);
            }
            return ret;
        }

        private System.WeakReference<ParsedDocument> parsedDocumentRef;
        public ParsedDocument RootParsedDocument
        {
            get
            {
                ParsedDocument ret;
                if (!parsedDocumentRef.TryGetTarget(out ret)) return null;
                return ret;
            }
            protected set
            {
                parsedDocumentRef = new WeakReference<ParsedDocument>(value);
            }
        }

        public List<int> Indexs = new List<int>();

        public bool IsGreaterThan(IndexReference indexReference)
        {
            if (indexReference == null) return false;

            int i = Indexs.Count;
            if( i > indexReference.Indexs.Count)
            {
                i = indexReference.Indexs.Count;
            }

            for(int j=0; j < i; j++)
            {
                if (Indexs[j] > indexReference.Indexs[j]) return true;
            }
            return false;
        }

        public bool IsSmallerThan(IndexReference indexReference)
        {
            if (indexReference == null) return false;

            int i = Indexs.Count;
            if (i > indexReference.Indexs.Count)
            {
                i = indexReference.Indexs.Count;
            }

            for (int j = 0; j < i; j++)
            {
                if (Indexs[j] < indexReference.Indexs[j]) return true;
            }
            return false;
        }

        public bool IsPointSameHier(IndexReference indexReference)
        {
            if (indexReference == null) return false;

            int i = Indexs.Count;
            if (i > indexReference.Indexs.Count)
            {
                i = indexReference.Indexs.Count;
            }

            for (int j = 0; j < i-1; j++)
            {
                if (Indexs[j] != indexReference.Indexs[j]) return false;
            }
            return true;

        }
    }
}
