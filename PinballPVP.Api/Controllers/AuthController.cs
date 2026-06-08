using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Data;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Services.Auth;
using PinballPVP.Api.Services.Password;

namespace PinballPVP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    PinballPVPContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    private readonly PinballPVPContext _context = context;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;

    // POST /api/auth
    [HttpPost]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(user => user.Username == dto.Username)
            .FirstOrDefaultAsync();

        if (user == null || !_passwordHasher.Verify(user.PasswordHash, dto.Password))
            return Unauthorized("Invalid username or password");

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new LoginResponseDto(token));
    }
}