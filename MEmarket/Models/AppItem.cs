using System.Collections.Generic;

namespace MEmarket.Models
{
    public class AppItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Publisher { get; set; }
        public string Category { get; set; }
        public string AppUrl { get; set; }
        public string AppType { get; set; }
        public string LatestVersion { get; set; }
        public string RepositoryName { get; set; }
        
        public List<AppVersion> Versions { get; set; }
        public List<string> Capabilities { get; set; }
        public List<string> Screenshots { get; set; }
        
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Size { get; set; }
    }

    public class AppVersion
    {
        public string Version { get; set; }
        public string AppFile { get; set; }
    }

    public class CategoryGroup
    {
        public string Title { get; set; }
        public List<AppItem> Items { get; set; }
    }
}
