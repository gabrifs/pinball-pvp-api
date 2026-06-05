namespace PinballPVP.Api.Models;

// User <---> PlayerRecord (One-to-One)
public class PlayerRecord()
{
    public int UserId { get; set; }

    public int SoloWins { get; set; }
    public int SoloLosses { get; set; }
    public int SoloHighscore { get; set; }

    public int VersusWins { get; set; }
    public int VersusLosses { get; set; }
    public int VersusHighscore { get; set; }

    public User User { get; set; } = null!; // This is PlayerRecord's identity
}