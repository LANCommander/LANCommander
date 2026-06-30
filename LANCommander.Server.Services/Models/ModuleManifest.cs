namespace LANCommander.Server.Models
{
    public class ModuleManifest
    {
        public string RootModule { get; set; } = string.Empty;
        public string ModuleVersion { get; set; } = "1.0.0";
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Author { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Copyright { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PowerShellVersion { get; set; } = string.Empty;
    }
}
