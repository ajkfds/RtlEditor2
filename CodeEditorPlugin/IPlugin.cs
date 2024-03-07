using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeEditorPlugin
{
    public interface IPlugin
    {
        bool Register();
        bool Initialize();
        string Id { get; }
    }
}
