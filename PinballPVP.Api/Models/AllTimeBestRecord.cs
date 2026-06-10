namespace PinballPVP.Api.Models;

// User <---> AllTimeBestRecord (One-to-One)
// Continuously-maintained personal bests, alongside the year each was set.
// PlayerRecord is year-to-date and resets on the yearly rollover; this table never resets.
public class AllTimeBestRecord()
{
    public int UserId { get; set; }

    public int SoloHighscore { get; set; }
    public int? SoloHighscoreYear { get; set; }

    public int SoloWins { get; set; }
    public int? SoloWinsYear { get; set; }

    public int SoloMatchesPlayed { get; set; }
    public int? SoloMatchesPlayedYear { get; set; }

    public int VersusHighscore { get; set; }
    public int? VersusHighscoreYear { get; set; }

    public int VersusWins { get; set; }
    public int? VersusWinsYear { get; set; }

    public int VersusMatchesPlayed { get; set; }
    public int? VersusMatchesPlayedYear { get; set; }

    public User User { get; set; } = null!; // This is AllTimeBestRecord's identity

    // Overwrites a metric (and its year) only if the new PlayerRecord value exceeds the stored best.
    public void UpdateFromSolo(PlayerRecord record, int year)
    {
        if (record.SoloHighscore > SoloHighscore)
        {
            SoloHighscore = record.SoloHighscore;
            SoloHighscoreYear = year;
        }

        if (record.SoloWins > SoloWins)
        {
            SoloWins = record.SoloWins;
            SoloWinsYear = year;
        }

        var matchesPlayed = record.SoloWins + record.SoloLosses;
        if (matchesPlayed > SoloMatchesPlayed)
        {
            SoloMatchesPlayed = matchesPlayed;
            SoloMatchesPlayedYear = year;
        }
    }

    public void UpdateFromVersus(PlayerRecord record, int year)
    {
        if (record.VersusHighscore > VersusHighscore)
        {
            VersusHighscore = record.VersusHighscore;
            VersusHighscoreYear = year;
        }

        if (record.VersusWins > VersusWins)
        {
            VersusWins = record.VersusWins;
            VersusWinsYear = year;
        }

        var matchesPlayed = record.VersusWins + record.VersusLosses;
        if (matchesPlayed > VersusMatchesPlayed)
        {
            VersusMatchesPlayed = matchesPlayed;
            VersusMatchesPlayedYear = year;
        }
    }
}
