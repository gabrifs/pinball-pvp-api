namespace PinballPVP.Api.Dtos;

public record UpdatePlayerRecordResponseDto(
    int UserId,
    
    int SoloWins,
    int SoloLosses,
    int SoloHighscore,

    int VersusWins,
    int VersusLosses,
    int VersusHighscore
);