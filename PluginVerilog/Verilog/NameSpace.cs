using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.DataObjects.Nets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pluginVerilog.Verilog
{
    public class NameSpace : Item
    {
        private NameSpace() { }
        protected NameSpace(BuildingBlocks.BuildingBlock buildingBlock,NameSpace parent)
        {
            BuildingBlock = buildingBlock;
            Parent = parent;
        }

        public IndexReference BeginIndexReference = null;
        public IndexReference LastIndexReference = null;

        private Dictionary<string, DataObjects.DataObject> variables = new Dictionary<string, DataObjects.DataObject>();
        private Dictionary<string, Net> nets = new Dictionary<string, Net>();
        private Dictionary<string, DataObjects.Typedef> typedefs = new Dictionary<string, DataObjects.Typedef>();

        private Dictionary<string, NameSpace> nameSpaces = new Dictionary<string, NameSpace>();

        public Dictionary<string, DataObjects.DataObject> DataObjects { get { return variables; } }


        public NameSpace Parent { get; protected set; }

        private Dictionary<string, DataObjects.Constants.Constants> constants = new Dictionary<string, DataObjects.Constants.Constants>();
        public Dictionary<string, DataObjects.Constants.Constants> Constants { get { return constants; } }

        public Dictionary<string, DataObjects.Typedef> Typedefs { get { return typedefs; } }
        public BuildingBlocks.BuildingBlock BuildingBlock { get; protected set; }
        public Dictionary<string, NameSpace> NameSpaces { get { return nameSpaces;  } }

        public NameSpace GetHierNameSpace(int index)
        {
/*            foreach(NameSpace subSpace in NameSpaces.Values)
            {
                if (index < subSpace.BeginIndex) continue;
                if (index > subSpace.LastIndex) continue;
                return subSpace.GetHierNameSpace(index);
            }
*/            return this;
        }
        

        private codeEditor.CodeEditor.AutocompleteItem newItem(string text, CodeDrawStyle.ColorType colorType)
        {
            return new codeEditor.CodeEditor.AutocompleteItem(text, CodeDrawStyle.ColorIndex(colorType), Global.CodeDrawStyle.Color(colorType));
        }
        public virtual void AppendAutoCompleteItem( List<codeEditor.CodeEditor.AutocompleteItem> items)
        {
            //foreach (Net net in Nets.Values)
            //{
            //    if (net is Net)
            //    {
            //        items.Add(newItem(net.Name, CodeDrawStyle.ColorType.Net));
            //    }
            //}

            foreach (DataObjects.DataObject variable in DataObjects.Values)
            {
                if(variable is DataObjects.Nets.Net)
                {
                    items.Add(newItem(variable.Name, CodeDrawStyle.ColorType.Net));
                }
                else if (variable is DataObjects.Variables.Reg)
                {
                    items.Add(newItem(variable.Name, CodeDrawStyle.ColorType.Register));
                }
                else if (variable is DataObjects.Variables.Integer)
                {
                    items.Add(newItem(variable.Name, CodeDrawStyle.ColorType.Variable));
                }
                else if (variable is DataObjects.Variables.Time || variable is DataObjects.Variables.Real || variable is DataObjects.Variables.Realtime || variable is DataObjects.Variables.Integer || variable is DataObjects.Variables.Genvar)
                {
                    items.Add(newItem(variable.Name, CodeDrawStyle.ColorType.Variable));
                }
            }

            foreach (DataObjects.Constants.Constants constants in BuildingBlock.Constants.Values)
            {
                items.Add(newItem(constants.Name, CodeDrawStyle.ColorType.Paramater));
            }

            foreach (Function function in BuildingBlock.Functions.Values)
            {
                items.Add(newItem(function.Name, CodeDrawStyle.ColorType.Identifier));
            }

            foreach (Task task in BuildingBlock.Tasks.Values)
            {
                items.Add(newItem(task.Name, CodeDrawStyle.ColorType.Identifier));
            }

            foreach (NameSpace space in NameSpaces.Values)
            {
                if (space.Name == null) System.Diagnostics.Debugger.Break();
                items.Add(newItem(space.Name, CodeDrawStyle.ColorType.Identifier));
            }

            if(Parent != null)
            {
                Parent.AppendAutoCompleteItem(items);
            }
        }

        public DataObjects.DataObject GetDataObject(string identifier)
        {
            if (DataObjects.ContainsKey(identifier))
            {
                return DataObjects[identifier];
            }

            if (Parent != null)
            {
                return Parent.GetDataObject(identifier);
            }
            return null;
        }

        public DataObjects.Constants.Constants GetConstants(string identifier)
        {
            if (Constants.ContainsKey(identifier))
            {
                return Constants[identifier];
            }

            if (Parent != null)
            {
                return Parent.getConstantsHier(identifier);
            }
            else
            {
                
            }

            return null;
        }

        private DataObjects.Constants.Constants getConstantsHier(string identifier)
        {
            if (Constants.ContainsKey(identifier))
            {
                return Constants[identifier];
            }

            if (Parent != null)
            {
                return Parent.getConstantsHier(identifier);
            }

            return null;
        }
    }
}
