using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record PlayerRecordResponseDto(
    int UserId,
    
    int SoloWins,
    int SoloLosses,
    int SoloHighscore,

    int VersusWins,
    int VersusLosses,
    int VersusHighscore
)
{
    public static readonly Expression<Func<PlayerRecord, PlayerRecordResponseDto>>
        Projection =
            playerRecord => new PlayerRecordResponseDto
            (
                playerRecord.UserId,

                playerRecord.SoloWins,
                playerRecord.SoloLosses,
                playerRecord.SoloHighscore,

                playerRecord.VersusWins,
                playerRecord.VersusLosses,
                playerRecord.VersusHighscore
            );

    public static PlayerRecordResponseDto FromEntity(PlayerRecord playerRecord)
    {
        return new PlayerRecordResponseDto
            (
                playerRecord.UserId,

                playerRecord.SoloWins,
                playerRecord.SoloLosses,
                playerRecord.SoloHighscore,

                playerRecord.VersusWins,
                playerRecord.VersusLosses,
                playerRecord.VersusHighscore
            );
    }
}