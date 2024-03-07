using Avalonia.Controls;
using RtlEditor2.Models.Common;
using static System.Net.Mime.MediaTypeNames;
using System.Collections.Generic;
using System;
using Avalonia.Threading;
using Avalonia;
using System.Security.Cryptography.X509Certificates;

namespace RtlEditor2.Tools
{
    public partial class ParseProjectForm : Window
    {
        public ParseProjectForm()
        {
            InitializeComponent();
        }

        DispatcherTimer timer = new DispatcherTimer();

        public ParseProjectForm(NavigatePanel.ProjectNode projectNode)
        {
            InitializeComponent();
            Title = projectNode.Project.Name;
            this.projectNode = projectNode;
            //            this.Icon = ajkControls.Global.Icon;
            this.ShowInTaskbar = false;
            Closing += ParseProjectForm_Closing;
            Loaded += ParseProjectForm_Loaded;
            Opened += ParseProjectForm_Opened;

            timer.Interval = new TimeSpan(0, 0, 0, 10);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            ProgressBar0.InvalidateVisual();
        }

        private void ParseProjectForm_Opened(object? sender, EventArgs e)
        {
            if (thread != null) return;
            thread = new System.Threading.Thread(() => { worker(); });
            thread.Start();
        }

        System.Threading.Thread thread = null;
        private void ParseProjectForm_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
        }



        private NavigatePanel.ProjectNode projectNode = null;
        private volatile bool close = false;


        private void ParseProjectForm_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (!close) e.Cancel = true;
            projectNode = null;
        }

        private void worker()
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            projectNode.Project.Update();

            {
                // data update
                projectNode.HierarchicalVisibleUpdate();
                List<Data.Item> items = projectNode.Project.FindItems(
                    (x) => (x is Data.TextFile),
                    (x) => (false)
                    );

                Dispatcher.UIThread.Post(new Action(() => { ProgressBar0.Maximum = items.Count; }));

                // parse items
                int i = 0;
                int workerThreads = 1;

                System.Collections.Concurrent.BlockingCollection<Data.TextFile> fileQueue = new System.Collections.Concurrent.BlockingCollection<Data.TextFile>();

                List<TextParseTask> tasks = new List<TextParseTask>();
                for (int t = 0; t < workerThreads; t++)
                {
                    tasks.Add(new TextParseTask());
                    tasks[t].Run(
                        fileQueue,
                        (
                            (f) =>
                            {
                                Dispatcher.UIThread.Post(
                                    new Action(() =>
                                    {
                                        ProgressBar0.Value = i;
                                        Message.Text = f.Name;
                                        i++;
                                    })
                                    );
                            }
                        )
                    );
                }

                foreach (Data.Item item in items)
                {
                    if (!(item is Data.TextFile)) continue;
                    fileQueue.Add(item as Data.TextFile);
                }
                fileQueue.CompleteAdding();

                while (!fileQueue.IsCompleted)
                {
                    System.Threading.Thread.Sleep(1);
                }

                while (true)
                {
                    int completeTasks = 0;
                    foreach (TextParseTask task in tasks)
                    {
                        if (task.Complete) completeTasks++;
                    }
                    if (completeTasks == tasks.Count) break;
                }


                //    gc++;
                //    if (gc > 100)
                //    {
                //        System.GC.Collect();
                //        gc = 0;
                //        System.Diagnostics.Debug.Print("process memory " + (Environment.WorkingSet / 1024 / 1024).ToString() + "Mbyte");
                //    }
                //}
            }

            System.Diagnostics.Debug.Print(projectNode.Project.Name + ":" + sw.ElapsedMilliseconds.ToString() + "ms");
            close = true;
//            Dispatcher.UIThread.Post(new Action(() => { Close(); }));
            Dispatcher.UIThread.Invoke(new Action(()=>{ Close(); }));
        }

    }
}
