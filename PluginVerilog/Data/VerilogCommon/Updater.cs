using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codeEditor.CodeEditor;
using codeEditor.Data;
using pluginVerilog.Verilog.BuildingBlocks;
using pluginVerilog.Verilog.ModuleItems;

namespace pluginVerilog.Data.VerilogCommon
{
    public static class Updater
    {
        public static void Update(IVerilogRelatedFile rootItem)
        {
            Project project = rootItem.Project;
            string moduleName = null;
            if (rootItem is VerilogModuleInstance)
            {
                moduleName = (rootItem as VerilogModuleInstance).ModuleName;
            }



            lock (rootItem.Items)
            {
                if (rootItem.VerilogParsedDocument == null)
                {
                    // dispose all subnodes
                    foreach (Item item in rootItem.Items.Values) item.Dispose();
                    rootItem.Items.Clear();
                    return;
                }

                List<Item> targetItems = new List<Item>();
                Dictionary<string, Item> newItems = new Dictionary<string, Item>();

                // include file
                Dictionary<string, VerilogHeaderInstance> prevIncludes = new Dictionary<string, VerilogHeaderInstance>();
                foreach (Item item in rootItem.Items.Values)
                {
                    if (item is VerilogHeaderInstance)
                    {
                        VerilogHeaderInstance vfile = item as VerilogHeaderInstance;
                        prevIncludes.Add(vfile.ID, vfile);
                    }
                }

                // include file
                foreach (VerilogHeaderInstance vhFile in rootItem.VerilogParsedDocument.IncludeFiles.Values)
                {
                    if (prevIncludes.ContainsKey(vhFile.ID))
                    {
                        prevIncludes[vhFile.ID].ReplaceBy(vhFile);
                        targetItems.Add(prevIncludes[vhFile.ID]);
                    }
                    else
                    {
                        string keyname = vhFile.Name;
                        {
                            int i = 0;
                            while (rootItem.Items.ContainsKey(keyname + "_" + i.ToString()))
                            {
                                i++;
                            }
                            keyname = keyname + "_" + i.ToString();
                        }
                        newItems.Add(keyname, vhFile);
                        targetItems.Add(vhFile);
                        vhFile.Parent = rootItem as Item;
                    }
                }

                // module instances
                if (rootItem.VerilogParsedDocument.Root != null) {
                    foreach (BuildingBlock module in rootItem.VerilogParsedDocument.Root.BuldingBlocks.Values)
                    {
                        if (moduleName == null || moduleName == module.Name)
                        {
                            UpdateModuleInstance(module, project, rootItem, targetItems, newItems);
                        }
                    }
                }

                { // remove unused items
                    List<Item> removeItems = new List<Item>();
                    foreach (codeEditor.Data.Item item in rootItem.Items.Values)
                    {
                        if (!targetItems.Contains(item)) removeItems.Add(item);
                    }

                    foreach (Item item in removeItems)
                    {
                        rootItem.Items.Remove(item.Name);
                    }
                }

                rootItem.Items.Clear();
                foreach (Item item in targetItems)
                {
                    rootItem.Items.Add(item.Name, item);
                }
            }
        }

        private static void UpdateModuleInstance(BuildingBlock module,Project project, IVerilogRelatedFile rootItem, List<Item> targetItems,Dictionary<string, Item> newItems)
        {
            foreach (Verilog.ModuleItems.IInstantiation subModuleInstantiation in module.Instantiations.Values)
            {
                if (rootItem.Items.ContainsKey(subModuleInstantiation.Name))
                { // already exist item
                    Item oldItem = rootItem.Items[subModuleInstantiation.Name];
                    if (oldItem is Data.VerilogModuleInstance && (oldItem as Data.VerilogModuleInstance).ReplaceBy(subModuleInstantiation as ModuleInstantiation, project))
                    { // sucessfully replaced
                        targetItems.Add(oldItem);
                    }
                    else
                    { // re-generate (same module instance name, but different file or module name or parameter
                        Item item = null;
                        if(subModuleInstantiation is ModuleInstantiation)
                        {
                            item = Data.VerilogModuleInstance.Create(subModuleInstantiation as ModuleInstantiation, project);
                        }
                        if (item != null & !newItems.ContainsKey(subModuleInstantiation.Name))
                        {
                            item.Parent = item;
                            newItems.Add(subModuleInstantiation.Name, item);
                            targetItems.Add(item);
                            if (subModuleInstantiation.ParameterOverrides.Count != 0)
                            {
                                Data.VerilogModuleInstance moduleInstance = item as Data.VerilogModuleInstance;
                                if (moduleInstance.ParsedDocument == null)
                                { // background reparse if not parsed
                                    project.AddReparseTarget(item);
                                }
                            }
                        }
                    }
                }
                else
                { // new item
                    Item item = null;
                    if (subModuleInstantiation is ModuleInstantiation)
                    {
                        item = Data.VerilogModuleInstance.Create(subModuleInstantiation as ModuleInstantiation, project);
                    }
                    if (item != null & !newItems.ContainsKey(subModuleInstantiation.Name))
                    {
                        item.Parent = rootItem as Item;
                        newItems.Add(subModuleInstantiation.Name, item);
                        targetItems.Add(item);
                        if (subModuleInstantiation.ParameterOverrides.Count != 0)
                        {
                            Data.VerilogModuleInstance moduleInstance = item as Data.VerilogModuleInstance;

                            if (moduleInstance.ParsedDocument == null)
                            {   // background reparse
                                project.AddReparseTarget(item);
                            }
                        }
                    }
                }
            }
        }

    }
}
