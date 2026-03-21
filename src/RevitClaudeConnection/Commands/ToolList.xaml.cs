using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using static RevitClaudeConnector.ToolHandler.ToolRegistry;

namespace RevitClaudeConnector
{
    public partial class ToolList : Window
    {
        public ObservableCollection<ToolDescriptor> AllTools { get; set; }
        public ObservableCollection<ToolDescriptor> FilteredTools { get; set; }

        public ToolList(IReadOnlyDictionary<string, ToolDescriptor> Tools)
        {
            InitializeComponent();

            AllTools = new ObservableCollection<ToolDescriptor>(Tools.Values);
            FilteredTools = new ObservableCollection<ToolDescriptor>(AllTools);
            DataContext = this;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            FilteredTools.Clear();

            foreach (var tool in AllTools
                     .Where(t =>
                         t.ToolSchema.Name.ToLower().Contains(filter) ||
                         t.ToolSchema.Description.ToLower().Contains(filter)))
            {
                FilteredTools.Add(tool);
            }
        }

        private void ViewSchema_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ToolDescriptor tool)
            {
                string schemaJson = tool.ToolSchema.InputSchema.ToString(Newtonsoft.Json.Formatting.Indented);
                MessageBox.Show(schemaJson, $"{tool.ToolSchema.Name} Schema", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}

