using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Services.SoloMatches;

public class SoloMatchService(PinballPVPContext context) : ISoloMatchService
{
    // A retried request with identical payload within this window is treated as the same match — the
    // original response was lost in transit. Prevents double-counting wins/losses on client retries.
    private const int DeduplicationWindowSeconds = 60;
    public async Task<PagedResult<SoloMatchResponseDto>> GetMatchesAsync(
        string? period, int page, int pageSize, CancellationToken ct = default)
    {
        return await context.SoloMatches
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<SoloMatchResponseDto?> GetMatchAsync(int id, CancellationToken ct = default)
    {
        return await context.SoloMatches
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(SoloMatchResponseDto.Projection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<SoloMatchResponseDto>?> GetUserMatchesAsync(
        int userId, string? period, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, ct))
            return null;

        return await context.SoloMatches
            .Where(m => m.UserId == userId)
            .ApplyPeriodFilter(period)
            .AsNoTracking()
            .OrderByDescending(m => m.PlayedAt)
            .Select(SoloMatchResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<CreateSoloMatchResult> CreateMatchAsync(CreateSoloMatchDto dto, CancellationToken ct = default)
    {
        var user = await context.Users
            .Include(u => u.PlayerRecord)
            .Include(u => u.AllTimeBestRecord)
            .FirstOrDefaultAsync(u => u.Id == dto.UserId, ct);

        if (user == null)
            return CreateSoloMatchResult.Failure(CreateSoloMatchError.UserNotFound);

        var cutoff = DateTime.UtcNow.AddSeconds(-DeduplicationWindowSeconds);
        var existing = await context.SoloMatches
            .AsNoTracking()
            .Where(m => m.UserId == dto.UserId
                     && m.FinalScore == dto.FinalScore
                     && m.RoundsWon == dto.RoundsWon
                     && m.HasWon == dto.HasWon
                     && m.PlayedAt > cutoff)
            .Select(SoloMatchResponseDto.Projection)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
            return CreateSoloMatchResult.Success(existing);

        var match = new SoloMatch
        {
            UserId = dto.UserId,
            User = user,
            FinalScore = dto.FinalScore,
            RoundsWon = dto.RoundsWon,
            HasWon = dto.HasWon,
            PlayedAt = DateTime.UtcNow
        };

        if (match.HasWon)
            user.PlayerRecord.SoloWins++;
        else
            user.PlayerRecord.SoloLosses++;

        user.PlayerRecord.SoloHighscore = Math.Max(user.PlayerRecord.SoloHighscore, dto.FinalScore);

        user.AllTimeBestRecord.UpdateFromSolo(user.PlayerRecord, match.PlayedAt.Year);

        context.SoloMatches.Add(match);
        await context.SaveChangesAsync(ct);

        return CreateSoloMatchResult.Success(SoloMatchResponseDto.FromEntity(match));
    }
}
