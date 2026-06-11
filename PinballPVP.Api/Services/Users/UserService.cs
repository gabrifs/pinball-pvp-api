using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Password;

namespace PinballPVP.Api.Services.Users;

public class UserService(PinballPVPContext context, IPasswordHasher passwordHasher) : IUserService
{
    public async Task<PagedResult<UserResponseDto>> GetUsersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(UserResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    public async Task<UserResponseDto?> GetUserAsync(int id, CancellationToken ct = default)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(UserResponseDto.Projection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CreateUserResult> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        if (await context.Users.AnyAsync(user => user.Username == dto.Username, ct))
            return CreateUserResult.Failure(CreateUserError.UsernameInUse);

        if (await context.Users.AnyAsync(user => user.Nickname == dto.Nickname, ct))
            return CreateUserResult.Failure(CreateUserError.NicknameInUse);

        if (await context.Users.AnyAsync(user => user.Email == dto.Email, ct))
            return CreateUserResult.Failure(CreateUserError.EmailInUse);

        var user = new User
        {
            Username = dto.Username,
            Nickname = dto.Nickname,
            Email = dto.Email,
            PasswordHash = passwordHasher.Hash(dto.Password),

            PlayerRecord = new PlayerRecord(),
            AllTimeBestRecord = new AllTimeBestRecord()
        };

        context.Users.Add(user);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pgEx)
        {
            return pgEx.ConstraintName switch
            {
                "ix_users_username" => CreateUserResult.Failure(CreateUserError.UsernameInUse),
                "ix_users_nickname" => CreateUserResult.Failure(CreateUserError.NicknameInUse),
                "ix_users_email"    => CreateUserResult.Failure(CreateUserError.EmailInUse),
                _                   => CreateUserResult.Failure(CreateUserError.DuplicateDetails)
            };
        }

        return CreateUserResult.Success(UserResponseDto.FromEntity(user));
    }

    public async Task<UpdateNicknameResult> UpdateNicknameAsync(int userId, UpdateNicknameDto dto, CancellationToken ct = default)
    {
        var user = await context.Users.FindAsync([userId], ct);
        if (user == null)
            return UpdateNicknameResult.Failure(UpdateNicknameError.UserNotFound);

        if (await context.Users.AnyAsync(u => u.Nickname == dto.Nickname && u.Id != userId, ct))
            return UpdateNicknameResult.Failure(UpdateNicknameError.NicknameInUse);

        user.Nickname = dto.Nickname;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "ix_users_nickname" })
        {
            return UpdateNicknameResult.Failure(UpdateNicknameError.NicknameInUse);
        }

        return UpdateNicknameResult.Success(UserResponseDto.FromEntity(user));
    }
}
