using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record CreateVersusMatchDto(
    [Range(1, int.MaxValue)] int WinnerId,
    [Range(0, int.MaxValue)] int WinnerFinalScore,
    [Range(0, int.MaxValue)] int WinnerRoundsWon,

    [Range(1, int.MaxValue)] int LoserId,
    [Range(0, int.MaxValue)] int LoserFinalScore,
    [Range(0, int.MaxValue)] int LoserRoundsWon
);