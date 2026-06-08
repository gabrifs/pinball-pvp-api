using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Models;
using PinballPVP.Api.Services.Password;
using PinballPVP.Api.Services.RateLimiting;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly PinballPVPContext _context;
    private IPasswordHasher _passwordHasher;

    public UsersController(PinballPVPContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // GET /api/users
    [HttpGet]
    public async Task<ActionResult<List<UserResponseDto>>> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Select(UserResponseDto.Projection)
            .ToListAsync();

        return Ok(users);
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

        await _context.SaveChangesAsync();

        return CreatedAtAction
        (
            nameof(GetUser),
            new { id = user.Id },
            UserResponseDto.FromEntity(user)
        );
    }
}