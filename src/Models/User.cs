namespace FlexFamilyCalendar.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";  // BCrypt hash — never plaintext
    public UserRole Role { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
}
