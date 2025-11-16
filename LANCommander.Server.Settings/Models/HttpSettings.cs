namespace LANCommander.Server.Settings.Models;

public class HttpSettings
{
    public int Port { get; set; } = 1337;
    public int SSLPort { get; set; } = 31337;
    public bool UseSSL { get; set; } = false;
    public string CertificatePath { get; set; } = String.Empty;
    public string CertificatePassword { get; set; } = String.Empty;
}