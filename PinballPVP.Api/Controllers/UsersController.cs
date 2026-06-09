using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Password;
using PinballPVP.Api.Services.RateLimiting;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsersController(PinballPVPContext context, IPasswordHasher passwordHasher) : ControllerBase
{
    private readonly PinballPVPContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    // GET /api/users
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetUsers(
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var result = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(UserResponseDto.Projection)
            .ToPagedResultAsync(page, pageSize);

        return Ok(result);
    }

    // GET /api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(int id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(UserResponseDto.Projection)
            .FirstOrDefaultAsync();

        if(user == null)
            return NotFound();

        return Ok(user);
    }

    // POST /api/users
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost]
    public async Task<ActionResult> CreateUser(CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(user => user.Username == dto.Username))
        {
            return BadRequest("Username already in use");
        }

        if(await _context.Users.AnyAsync(user => user.Nickname == dto.Nickname))
        {
            return BadRequest("Nickname already in use");
        }

        if(await _context.Users.AnyAsync(user => user.Email == dto.Email))
        {
            return BadRequest("Email already in use");
        }

        var user = new User
        {
            Username = dto.Username,
            Nickname = dto.Nickname,
            Email = dto.Email,
            PasswordHash =  _passwordHasher.Hash(dto.Password),

            PlayerRecord = new PlayerRecord()
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pgEx)
        {
            return pgEx.ConstraintName switch
            {
                "ix_users_username" => BadRequest("Username already in use"),
                "ix_users_nickname" => BadRequest("Nickname already in use"),
                "ix_users_email"    => BadRequest("Email already in use"),
                _                   => BadRequest("A user with these details already exists")
            };
        }

        return CreatedAtAction
        (
            nameof(GetUser),
            new { id = user.Id },
            UserResponseDto.FromEntity(user)
        );
    }
}