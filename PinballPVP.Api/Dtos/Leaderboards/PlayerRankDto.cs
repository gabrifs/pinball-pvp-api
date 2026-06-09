namespace PinballPVP.Api.Dtos.Leaderboards;

public record PlayerRankDto(
    int UserId,
    string Nickname,
    SoloRankDto? Solo,
    VersusRankDto? Versus);
