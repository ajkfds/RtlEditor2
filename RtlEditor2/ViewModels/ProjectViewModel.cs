using Avalonia.Controls;
using RtlEditor2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtlEditor2.ViewModels
{
    public class ProjectViewModel : ViewModelBase
    {
        public ObservableCollection<Node> Nodes { get; }

        public ProjectViewModel()
        {
            Nodes = new ObservableCollection<Node>
            {
                new Node("Animals", new ObservableCollection<Node>
                {
                    new Node("Mammals", new ObservableCollection<Node>
                    {
                        new Node("Lion"), new Node("Cat"), new Node("Zebra")
                    })
                })
            };
        }
    }
}
