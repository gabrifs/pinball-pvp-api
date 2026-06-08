namespace PinballPVP.Api.Dtos;

public record CreateSoloMatchDto(
    int UserId,
    int FinalScore,
    int RoundsWon,
    bool HasWon
);