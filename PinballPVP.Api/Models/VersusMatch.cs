namespace PinballPVP.Api.Models;

public class VersusMatch()
{
    public int Id { get; set; }

    public int WinnerId { get; set; } // Foreign key column
    public User Winner { get; set; } = null!; // Navigation Property
    
    public int WinnerFinalScore { get; set; }
    public int WinnerRoundsWon {get; set; }

    public int LoserId { get; set; } // Foreign key column
    public User Loser { get; set; } = null!; // Navigation Property

    public int LoserFinalScore { get; set; }
    public int LoserRoundsWon { get; set; }

    public DateTime PlayedAt { get; set; }
}