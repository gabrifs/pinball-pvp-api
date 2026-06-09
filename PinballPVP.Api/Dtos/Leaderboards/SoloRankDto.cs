namespace PinballPVP.Api.Dtos.Leaderboards;

public record SoloRankDto(
    int Highscore,
    int Wins,
    int Losses,
    double WinRate,
    int HighscoreRank,
    int WinsRank,
    int WinRateRank);
