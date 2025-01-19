namespace LANCommander.Launcher.Data.Models;

public class User : BaseModel
{
    public string UserName { get; set; }
    public string Alias { get; set; }
    public Media Avatar { get; set; }
    public Library Library { get; set; }
}