using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record SoloMatchDto(
    int Id,

    int UserId,
    int FinalScore,
    int RoundsWon,
    bool HasWon,
    
    DateTime PlayedAt
)
{
    public static readonly Expression<Func<SoloMatch, SoloMatchDto>>
        Projection =
            match => new SoloMatchDto
            (
                match.Id,
                match.UserId,
                match.FinalScore,
                match.RoundsWon,
                match.HasWon,
                match.PlayedAt
            );

    public static SoloMatchDto FromEntity(SoloMatch match)
    {
        return new SoloMatchDto
            (
                match.Id,
                match.UserId,
                match.FinalScore,
                match.RoundsWon,
                match.HasWon,
                match.PlayedAt
            );
    }
}