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
    int VersusHighscore,

    AllTimeBestRecordDto AllTimeBest
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
                playerRecord.VersusHighscore,

                new AllTimeBestRecordDto
                (
                    playerRecord.User.AllTimeBestRecord.SoloHighscore,
                    playerRecord.User.AllTimeBestRecord.SoloHighscoreYear,

                    playerRecord.User.AllTimeBestRecord.SoloWins,
                    playerRecord.User.AllTimeBestRecord.SoloWinsYear,

                    playerRecord.User.AllTimeBestRecord.SoloMatchesPlayed,
                    playerRecord.User.AllTimeBestRecord.SoloMatchesPlayedYear,

                    playerRecord.User.AllTimeBestRecord.VersusHighscore,
                    playerRecord.User.AllTimeBestRecord.VersusHighscoreYear,

                    playerRecord.User.AllTimeBestRecord.VersusWins,
                    playerRecord.User.AllTimeBestRecord.VersusWinsYear,

                    playerRecord.User.AllTimeBestRecord.VersusMatchesPlayed,
                    playerRecord.User.AllTimeBestRecord.VersusMatchesPlayedYear
                )
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
                playerRecord.VersusHighscore,

                AllTimeBestRecordDto.FromEntity(playerRecord.User.AllTimeBestRecord)
            );
    }
}