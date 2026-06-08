namespace PinballPVP.Api.Dtos;

public record CreateVersusMatchDto(
    int WinnerId,
    int WinnerFinalScore,
    int WinnerRoundsWon,
    
    int LoserId,
    int LoserFinalScore,
    int LoserRoundsWon
);