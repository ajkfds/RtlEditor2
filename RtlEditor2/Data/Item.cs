using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.Data
{
    public class Item : IDisposable
    {
        protected Item() { }

        private WeakReference<Item> parent;
        public Item Parent
        {
            get
            {
                Item ret;
                if (parent == null) return null;
                if (!parent.TryGetTarget(out ret)) return null;
                return ret;
            }
            set
            {
                parent = new WeakReference<Item>(value);
            }
        }

        public virtual string ID
        {
            get
            {
                return RelativePath;
            }
        }

        public virtual bool Ignore
        {
            get; set;
        }
        public virtual string RelativePath { get; protected set; }

        public virtual string Name { get; protected set; }
        public virtual Project Project { get; protected set; }

        protected ItemList items = new ItemList();
        public virtual ItemList Items
        {
            get { return items; }
        }


        public class ItemList
        {
            private List<Item> itemList = new List<Item>();
            private Dictionary<string, Item> itemDict = new Dictionary<string, Item>();

            public void Add(string key, Item item)
            {
                if (itemDict.ContainsKey(key)) return;
                itemList.Add(item);
                itemDict.Add(key, item);
            }

            public void Insert(int index, string key, Item item)
            {
                if (itemDict.ContainsKey(key)) return;
                itemList.Insert(index, item);
                itemDict.Add(key, item);
            }

            public int IndexOf(Item item)
            {
                return itemList.IndexOf(item);
            }

            public Item this[string key]
            {
                get
                {
                    return itemDict[key];
                }
            }

            public Item this[int index]
            {
                get
                {
                    return itemList[index];
                }
            }

            public void Remove(string key)
            {
                itemList.Remove(itemDict[key]);
                itemDict.Remove(key);
            }
            public bool ContainsKey(string key)
            {
                return itemDict.ContainsKey(key);
            }

            public bool ContainsValue(Item item)
            {
                return itemDict.ContainsValue(item);
            }

            public void Clear()
            {
                itemList.Clear();
                itemDict.Clear();
            }

            public Dictionary<string, Item>.KeyCollection Keys
            {
                get { return itemDict.Keys; }
            }

            public List<Item> Values
            {
                get
                {
                    return itemList;
                }
            }

        }


        public virtual Item GetItem(string relativePath)
        {
            // hier search to get a item
            if (Name == relativePath) return this;

            if (relativePath.Contains(@"\"))
            {
                string folderName = relativePath.Substring(0, relativePath.IndexOf(@"\"));
                if (items.ContainsKey(folderName))
                {
                    return items[folderName].GetItem(relativePath.Substring(folderName.Length + 1));
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (items.ContainsKey(relativePath))
                {
                    return items[relativePath];
                }
                else
                {
                    return null;
                }
            }
        }

        public virtual List<Item> FindItems(Func<Item, bool> match, Func<Item, bool> stop)
        {
            List<Item> result = new List<Item>();
            findItems(result, match, stop);
            return result;
        }

        protected void findItems(List<Item> result, Func<Item, bool> match, Func<Item, bool> stop)
        {
            lock (Items)
            {
                foreach (Item item in items.Values)
                {
                    if (match(item)) result.Add(item);
                    if (!stop(item)) item.findItems(result, match, stop);
                }
            }
        }


        public virtual void Dispose()
        {
        }

        public virtual void Update() { }


        protected WeakReference<NavigatePanel.NavigatePanelNode> nodeRef;
        public virtual NavigatePanel.NavigatePanelNode NavigatePanelNode
        {
            get
            {
                NavigatePanel.NavigatePanelNode node;
                if (nodeRef == null)
                {
                    node = createNode();
                    if (node == null) return null;
                    nodeRef = new WeakReference<NavigatePanel.NavigatePanelNode>(node);
                    return node;
                }

                if (nodeRef.TryGetTarget(out node)) return node;

                node = createNode();
                if (node == null) return null;
                nodeRef = new WeakReference<NavigatePanel.NavigatePanelNode>(node);
                return node;
            }
            protected set
            {
                nodeRef = new WeakReference<NavigatePanel.NavigatePanelNode>(value);
            }
        }
        protected virtual NavigatePanel.NavigatePanelNode createNode()
        {
            // should set nodeRef
            System.Diagnostics.Debugger.Break();
            return NavigatePanelNode;
        }

        public virtual NavigatePanel.NavigatePanelNode CreateLinkNode()
        {
            NavigatePanel.NavigatePanelNode node;
            node = createNode();
            if (node != null) node.Link = true;
            return node;
        }

        //public virtual CodeEditor.DocumentParser CreateDocumentParser(CodeEditor.DocumentParser.ParseModeEnum parseMode)
        //{
        //    return null;
        //}


    }
}
