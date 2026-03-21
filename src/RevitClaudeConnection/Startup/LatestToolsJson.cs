namespace RevitClaudeConnector.Startup
{
    public sealed class LatestToolsJson
    {
        public string version { get; set; } = "";
        public string zip_url { get; set; } = "";
        public string? sha256 { get; set; }
        // add fields if you want (e.g., "notes", "min_plugin", etc.)
    }
}