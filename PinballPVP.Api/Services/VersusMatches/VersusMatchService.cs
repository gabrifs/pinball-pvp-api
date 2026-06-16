using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.VersusMatches;

public class VersusMatchService(PinballPVPContext context) : IVersusMatchService
{
    // See SoloMatchService for rationale — same dedup window applied to the second-reporter path.
    private const int DeduplicationWindowSeconds = 60;
    public async Task<PagedResult<VersusMatchResponseDto>> GetMatchesAsync(
        string? period, int page, int pageSize, CancellationToken ct = default)
    {
        return await context.VersusMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<VersusMatchResponseDto?> GetMatchAsync(int id, CancellationToken ct = default)
    {
        return await context.VersusMatches
            .AsNoTracking()
            .Where(match => match.Id == id)
            .Select(VersusMatchResponseDto.Projection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<VersusMatchResponseDto>?> GetUserMatchesAsync(
        int userId, string? period, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, ct))
            return null;

        return await context.VersusMatches
            .Where(m => m.WinnerId == userId || m.LoserId == userId)
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(VersusMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<CreateVersusMatchResult> CreateMatchAsync(int reporterId, CreateVersusMatchDto dto, CancellationToken ct = default)
    {
        var users = await context.Users
            .Include(user => user.PlayerRecord)
            .Include(user => user.AllTimeBestRecord)
            .Where(user => user.Id == dto.WinnerId || user.Id == dto.LoserId)
            .ToListAsync(ct);

        var winner = users.FirstOrDefault(user => user.Id == dto.WinnerId);
        var loser = users.FirstOrDefault(user => user.Id == dto.LoserId);

        if (winner == null || loser == null)
            return CreateVersusMatchResult.Failure(CreateVersusMatchError.UsersNotFound);

        var now = DateTime.UtcNow;
        var minPlayerId = Math.Min(dto.WinnerId, dto.LoserId);
        var maxPlayerId = Math.Max(dto.WinnerId, dto.LoserId);

        var pending = await context.PendingVersusMatches
            .Where(p => p.MinPlayerId == minPlayerId && p.MaxPlayerId == maxPlayerId)
            .FirstOrDefaultAsync(ct);

        // Treat an expired pending match as if it doesn't exist
        if (pending?.IsExpired == true)
        {
            context.PendingVersusMatches.Remove(pending);
            await context.SaveChangesAsync(ct);
            pending = null;
        }

        if (pending is not null)
        {
            if (pending.ReporterId == reporterId)
                return CreateVersusMatchResult.Failure(CreateVersusMatchError.AlreadyPending);

            // Second reporter: all six fields must match exactly
            var resultsMatch =
                pending.WinnerId         == dto.WinnerId         &&
                pending.LoserId          == dto.LoserId          &&
                pending.WinnerFinalScore == dto.WinnerFinalScore &&
                pending.WinnerRoundsWon  == dto.WinnerRoundsWon  &&
                pending.LoserFinalScore  == dto.LoserFinalScore  &&
                pending.LoserRoundsWon   == dto.LoserRoundsWon;

            context.PendingVersusMatches.Remove(pending);

            if (!resultsMatch)
            {
                await context.SaveChangesAsync(ct);
                return CreateVersusMatchResult.Failure(CreateVersusMatchError.ResultsMismatch);
            }

            // Dedup: a prior second-reporter request may have already committed this match (response
            // lost in transit → both players retry → this path runs again). Return the existing match
            // after cleaning up the re-created pending record; don't double-count stats.
            var cutoff = now.AddSeconds(-DeduplicationWindowSeconds);
            var existingMatch = await context.VersusMatches
                .AsNoTracking()
                .Where(m => m.WinnerId == dto.WinnerId
                         && m.LoserId == dto.LoserId
                         && m.WinnerFinalScore == dto.WinnerFinalScore
                         && m.WinnerRoundsWon == dto.WinnerRoundsWon
                         && m.LoserFinalScore == dto.LoserFinalScore
                         && m.LoserRoundsWon == dto.LoserRoundsWon
                         && m.PlayedAt > cutoff)
                .Select(VersusMatchResponseDto.Projection)
                .FirstOrDefaultAsync(ct);

            if (existingMatch != null)
            {
                await context.SaveChangesAsync(ct); // persist pending match removal
                return CreateVersusMatchResult.Created(existingMatch);
            }

            // Both reporters agree — commit the match
            var match = new VersusMatch
            {
                WinnerId = dto.WinnerId,
                Winner = winner,
                WinnerFinalScore = dto.WinnerFinalScore,
                WinnerRoundsWon = dto.WinnerRoundsWon,

                LoserId = dto.LoserId,
                Loser = loser,
                LoserFinalScore = dto.LoserFinalScore,
                LoserRoundsWon = dto.LoserRoundsWon,

                PlayedAt = now
            };

            winner.PlayerRecord.VersusWins++;
            loser.PlayerRecord.VersusLosses++;

            winner.PlayerRecord.VersusHighscore = Math.Max(
                winner.PlayerRecord.VersusHighscore,
                dto.WinnerFinalScore);

            loser.PlayerRecord.VersusHighscore = Math.Max(
                loser.PlayerRecord.VersusHighscore,
                dto.LoserFinalScore);

            winner.AllTimeBestRecord.UpdateFromVersus(winner.PlayerRecord, now.Year);
            loser.AllTimeBestRecord.UpdateFromVersus(loser.PlayerRecord, now.Year);

            context.VersusMatches.Add(match);
            await context.SaveChangesAsync(ct);

            return CreateVersusMatchResult.Created(VersusMatchResponseDto.FromEntity(match));
        }

        // No active pending match — first reporter
        context.PendingVersusMatches.Add(new PendingVersusMatch
        {
            ReporterId = reporterId,
            MinPlayerId = minPlayerId,
            MaxPlayerId = maxPlayerId,
            WinnerId = dto.WinnerId,
            LoserId = dto.LoserId,
            WinnerFinalScore = dto.WinnerFinalScore,
            WinnerRoundsWon = dto.WinnerRoundsWon,
            LoserFinalScore = dto.LoserFinalScore,
            LoserRoundsWon = dto.LoserRoundsWon,
            ExpiresAt = now.AddMinutes(PendingVersusMatch.ConfirmationWindowMinutes)
        });

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Race: the other player submitted simultaneously — tell this player to retry as second reporter
            return CreateVersusMatchResult.Failure(CreateVersusMatchError.PendingConflict);
        }

        return CreateVersusMatchResult.AcceptedPending();
    }
}
