using System.Collections.Generic;

namespace RevitClaudePlugIn.Startup
{
    public sealed class GitHubRelease
    {
        public string tag_name { get; set; } = "";
        public string html_url { get; set; } = "";
        public string body { get; set; } = "";
        public List<GitHubAsset> assets { get; set; } = new();
    }

    public sealed class GitHubAsset
    {
        public string name { get; set; } = "";
        public string browser_download_url { get; set; } = "";
    }
}
