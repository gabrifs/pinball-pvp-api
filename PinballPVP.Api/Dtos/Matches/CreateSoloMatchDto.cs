using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record CreateSoloMatchDto(
    [Range(1, int.MaxValue)] int UserId,
    [Range(0, int.MaxValue)] int FinalScore,
    [Range(0, int.MaxValue)] int RoundsWon,
    [Required] bool HasWon
);