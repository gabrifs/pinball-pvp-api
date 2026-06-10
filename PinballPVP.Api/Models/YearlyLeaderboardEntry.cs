using PinballPVP.Api.Enums;

namespace PinballPVP.Api.Models;

// Top-3 snapshot per category per year, captured by the yearly rollover.
// UserId is nullable (DeleteBehavior.SetNull) so the historical entry survives account deletion;
// NicknameSnapshot freezes the display name as it was at capture time.
public class YearlyLeaderboardEntry()
{
    public int Id { get; set; }

    public int Year { get; set; }
    public YearlyLeaderboardCategory Category { get; set; }
    public int Rank { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public required string NicknameSnapshot { get; set; }
    public double Value { get; set; }
}
