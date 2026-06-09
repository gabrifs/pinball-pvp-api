namespace PinballPVP.Api.Models;

public class PendingVersusMatch
{
    public const int ConfirmationWindowMinutes = 5;

    public int Id { get; set; }
    public int ReporterId { get; set; }

    // Normalised pair (min/max of WinnerId/LoserId) — backing the unique index that
    // prevents duplicate pending matches for the same two players regardless of
    // who claims to have won.
    public int MinPlayerId { get; set; }
    public int MaxPlayerId { get; set; }

    public int WinnerId { get; set; }
    public int LoserId { get; set; }
    public int WinnerFinalScore { get; set; }
    public int WinnerRoundsWon { get; set; }
    public int LoserFinalScore { get; set; }
    public int LoserRoundsWon { get; set; }

    public DateTime ExpiresAt { get; set; }

    public User Reporter { get; set; } = null!;
    public User Winner { get; set; } = null!;
    public User Loser { get; set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
