using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Models;

public class PasswordRecoveryCode
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [MaxLength(64)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }

    public User User { get; set; } = null!;
}
