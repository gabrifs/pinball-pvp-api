using Microsoft.EntityFrameworkCore;

namespace PinballPVP.Api.Models;

// Unique Constraints
[Index(nameof(Username), IsUnique = true)]
[Index(nameof(Nickname), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class User()
{
    public int Id { get; set; }

    public required string Username { get; set; }
    public required string Nickname { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    public PlayerRecord PlayerRecord { get; set; } = null!; // One-to-One connection
}