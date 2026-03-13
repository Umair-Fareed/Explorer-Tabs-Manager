using System;
using System.Collections.Generic;

namespace ExplorerTabManager.Models
{
    public class TabState
    {
        public DateTime SavedAt { get; set; }
        public List<ExplorerWindow> Windows { get; set; } = new List<ExplorerWindow>();
    }

    public class ExplorerWindow
    {
        public List<string> TabPaths { get; set; } = new List<string>();
        public int ActiveTabIndex { get; set; }
    }
}
