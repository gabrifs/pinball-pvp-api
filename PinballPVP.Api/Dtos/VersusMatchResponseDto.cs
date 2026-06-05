using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record VersusMatchResponseDto(
    int Id,

    int WinnerId,
    string WinnerNickame,
    int WinnerFinalScore,
    int WinnerRoundsWon,

    int LoserId,
    string LoserNickame,
    int LoserFinalScore,
    int LoserRoundsWon,
    
    DateTime PlayedAt
)
{
    public static readonly Expression<Func<VersusMatch, VersusMatchResponseDto>>
        Projection =
            match => new VersusMatchResponseDto
            (
                match.Id,
                match.WinnerId,
                match.Winner.Nickname,
                match.WinnerFinalScore,
                match.WinnerRoundsWon,
                match.LoserId,
                match.Loser.Nickname,
                match.LoserFinalScore,
                match.LoserRoundsWon,
                match.PlayedAt
            );

    public static VersusMatchResponseDto FromEntity(VersusMatch match)
    {
        return new VersusMatchResponseDto
            (
                match.Id,
                match.WinnerId,
                match.Winner.Nickname,
                match.WinnerFinalScore,
                match.WinnerRoundsWon,
                match.LoserId,
                match.Loser.Nickname,
                match.LoserFinalScore,
                match.LoserRoundsWon,
                match.PlayedAt
            );
    }
}