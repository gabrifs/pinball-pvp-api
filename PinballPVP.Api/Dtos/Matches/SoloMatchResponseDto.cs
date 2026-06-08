using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record SoloMatchResponseDto(
    int Id,

    int UserId,
    int FinalScore,
    int RoundsWon,
    bool HasWon,
    
    DateTime PlayedAt
)
{
    public static readonly Expression<Func<SoloMatch, SoloMatchResponseDto>>
        Projection =
            match => new SoloMatchResponseDto
            (
                match.Id,
                match.UserId,
                match.FinalScore,
                match.RoundsWon,
                match.HasWon,
                match.PlayedAt
            );

    public static SoloMatchResponseDto FromEntity(SoloMatch match)
    {
        return new SoloMatchResponseDto
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