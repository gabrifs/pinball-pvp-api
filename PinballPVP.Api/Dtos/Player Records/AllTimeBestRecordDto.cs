using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record AllTimeBestRecordDto(
    int SoloHighscore,
    int? SoloHighscoreYear,

    int SoloWins,
    int? SoloWinsYear,

    int SoloMatchesPlayed,
    int? SoloMatchesPlayedYear,

    int VersusHighscore,
    int? VersusHighscoreYear,

    int VersusWins,
    int? VersusWinsYear,

    int VersusMatchesPlayed,
    int? VersusMatchesPlayedYear
)
{
    public static readonly Expression<Func<AllTimeBestRecord, AllTimeBestRecordDto>>
        Projection =
            bestRecord => new AllTimeBestRecordDto
            (
                bestRecord.SoloHighscore,
                bestRecord.SoloHighscoreYear,

                bestRecord.SoloWins,
                bestRecord.SoloWinsYear,

                bestRecord.SoloMatchesPlayed,
                bestRecord.SoloMatchesPlayedYear,

                bestRecord.VersusHighscore,
                bestRecord.VersusHighscoreYear,

                bestRecord.VersusWins,
                bestRecord.VersusWinsYear,

                bestRecord.VersusMatchesPlayed,
                bestRecord.VersusMatchesPlayedYear
            );

    public static AllTimeBestRecordDto FromEntity(AllTimeBestRecord bestRecord)
    {
        return new AllTimeBestRecordDto
            (
                bestRecord.SoloHighscore,
                bestRecord.SoloHighscoreYear,

                bestRecord.SoloWins,
                bestRecord.SoloWinsYear,

                bestRecord.SoloMatchesPlayed,
                bestRecord.SoloMatchesPlayedYear,

                bestRecord.VersusHighscore,
                bestRecord.VersusHighscoreYear,

                bestRecord.VersusWins,
                bestRecord.VersusWinsYear,

                bestRecord.VersusMatchesPlayed,
                bestRecord.VersusMatchesPlayedYear
            );
    }
}
