using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos.Leaderboards;

public record YearlyLeaderboardEntryDto(
    int Rank,
    int? UserId,
    string Nickname,
    double Value)
{
    public static readonly Expression<Func<YearlyLeaderboardEntry, YearlyLeaderboardEntryDto>>
        Projection =
            entry => new YearlyLeaderboardEntryDto
            (
                entry.Rank,
                entry.UserId,
                entry.NicknameSnapshot,
                entry.Value
            );

    public static YearlyLeaderboardEntryDto FromEntity(YearlyLeaderboardEntry entry)
    {
        return new YearlyLeaderboardEntryDto
            (
                entry.Rank,
                entry.UserId,
                entry.NicknameSnapshot,
                entry.Value
            );
    }
}
