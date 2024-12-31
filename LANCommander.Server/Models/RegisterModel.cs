using System.ComponentModel.DataAnnotations;
using LANCommander.Server.UI.Pages.Account;

namespace LANCommander.Server.Models;

public class RegisterModel
{
    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; }
    
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }
    
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string PasswordConfirmation { get; set; }
    
    [Display(Name = "Email")]
    public string? Email { get; set; }
    
    public RegistrationType RegistrationType { get; set; }
}