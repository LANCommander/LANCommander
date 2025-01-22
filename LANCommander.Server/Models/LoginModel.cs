using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.Models;

public class LoginModel
{
    [Required]
    [DataType(DataType.Text)]
    [Display(Name = "User Name")]
    public string Username { get; set; }
    
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }
    
    // public string ReturnUrl { get; set; }
    
    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}