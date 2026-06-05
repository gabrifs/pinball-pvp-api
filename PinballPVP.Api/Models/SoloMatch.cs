namespace PinballPVP.Api.Models;

public class SoloMatch()
{
    public int Id { get; set; }

    public int UserId { get; set; } // Foreign key column
    public User User { get; set; } = null!; // Navigation Property
    
    public bool HasWon { get; set; }
    public int FinalScore { get; set; }
    public int RoundsWon {get; set; }

    public DateTime PlayedAt { get; set; }
}