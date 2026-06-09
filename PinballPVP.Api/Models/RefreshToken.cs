using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive => DateTime.UtcNow < ExpiresAt && RevokedAt is null;
}
