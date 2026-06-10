namespace PinballPVP.Api.Dtos.Leaderboards;

public record YearlyLeaderboardResponseDto(
    int Year,
    List<YearlyLeaderboardEntryDto> SoloHighscore,
    List<YearlyLeaderboardEntryDto> SoloWins,
    List<YearlyLeaderboardEntryDto> SoloWinRate,
    List<YearlyLeaderboardEntryDto> VersusHighscore,
    List<YearlyLeaderboardEntryDto> VersusWins,
    List<YearlyLeaderboardEntryDto> VersusWinRate);
