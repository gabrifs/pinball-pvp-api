using System.Net;
using System.Net.Http.Json;
using PinballPVP.Api.Dtos;
using PinballPVP.Tests.Infrastructure;

namespace PinballPVP.Tests.Integration;

public class VersusMatchesTests(PinballApiFactory factory) : IntegrationTestBase(factory)
{
    private static CreateVersusMatchDto BuildDto(int winnerId, int loserId) =>
        new(winnerId, 8000, 3, loserId, 5000, 2);

    [Fact]
    public async Task CreateMatch_Unauthenticated_Returns401()
    {
        var (aliceId, _, _) = await RegisterAndLoginAsync();
        var (bobId, _, _)   = await RegisterAndLoginAsync();

        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_SelfVsSelf_Returns400()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        Authorize(aliceToken);

        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, aliceId));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_AsNonParticipant_Returns403()
    {
        var (aliceId, _, _)       = await RegisterAndLoginAsync();
        var (bobId, _, _)         = await RegisterAndLoginAsync();
        var (_, charlieToken, _)  = await RegisterAndLoginAsync();
        Authorize(charlieToken);

        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_FirstReport_Returns202Accepted()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        var (bobId, _, _)            = await RegisterAndLoginAsync();
        Authorize(aliceToken);

        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_SecondReport_MatchingResults_Returns201AndUpdatesRecords()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        var (bobId, bobToken, _)     = await RegisterAndLoginAsync();
        var dto = BuildDto(aliceId, bobId);

        Authorize(aliceToken);
        await Client.PostAsJsonAsync("/api/v1/versusmatches", dto);

        Authorize(bobToken);
        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", dto);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var aliceRecord = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
            $"/api/v1/users/playerrecords/{aliceId}");
        var bobRecord = await Client.GetFromJsonAsync<PlayerRecordResponseDto>(
            $"/api/v1/users/playerrecords/{bobId}");

        Assert.Equal(1, aliceRecord!.VersusWins);
        Assert.Equal(0, aliceRecord.VersusLosses);
        Assert.Equal(dto.WinnerFinalScore, aliceRecord.VersusHighscore);

        Assert.Equal(0, bobRecord!.VersusWins);
        Assert.Equal(1, bobRecord.VersusLosses);
        Assert.Equal(dto.LoserFinalScore, bobRecord.VersusHighscore);

        var currentYear = DateTime.UtcNow.Year;

        Assert.Equal(dto.WinnerFinalScore, aliceRecord.AllTimeBest.VersusHighscore);
        Assert.Equal(currentYear, aliceRecord.AllTimeBest.VersusHighscoreYear);
        Assert.Equal(1, aliceRecord.AllTimeBest.VersusWins);
        Assert.Equal(currentYear, aliceRecord.AllTimeBest.VersusWinsYear);
        Assert.Equal(1, aliceRecord.AllTimeBest.VersusMatchesPlayed);
        Assert.Equal(currentYear, aliceRecord.AllTimeBest.VersusMatchesPlayedYear);

        Assert.Equal(dto.LoserFinalScore, bobRecord.AllTimeBest.VersusHighscore);
        Assert.Equal(currentYear, bobRecord.AllTimeBest.VersusHighscoreYear);
        Assert.Equal(0, bobRecord.AllTimeBest.VersusWins);
        Assert.Null(bobRecord.AllTimeBest.VersusWinsYear);
        Assert.Equal(1, bobRecord.AllTimeBest.VersusMatchesPlayed);
        Assert.Equal(currentYear, bobRecord.AllTimeBest.VersusMatchesPlayedYear);
    }

    [Fact]
    public async Task CreateMatch_SecondReport_MismatchedResults_Returns409AndDiscardsSubmissions()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        var (bobId, bobToken, _)     = await RegisterAndLoginAsync();

        Authorize(aliceToken);
        await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));

        // Bob reports a different outcome — Alice loses instead of wins
        Authorize(bobToken);
        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(bobId, aliceId));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        // Pending cleared — Alice can start a fresh submission
        Authorize(aliceToken);
        var retry = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_SameReporterSubmitsTwice_Returns400()
    {
        var (aliceId, aliceToken, _) = await RegisterAndLoginAsync();
        var (bobId, _, _)            = await RegisterAndLoginAsync();
        Authorize(aliceToken);

        await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));
        var resp = await Client.PostAsJsonAsync("/api/v1/versusmatches", BuildDto(aliceId, bobId));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
