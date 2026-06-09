namespace PinballPVP.Api.Dtos.Leaderboards;

public record SoloLeaderboardEntryDto(
    int Rank,
    int UserId,
    string Nickname,
    int Highscore,
    int Wins,
    int Losses,
    double WinRate);
