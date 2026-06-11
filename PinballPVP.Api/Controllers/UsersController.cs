using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PinballPVP.Api.Dtos;
using PinballPVP.Api.Extensions;
using PinballPVP.Api.Services.RateLimiting;
using PinballPVP.Api.Services.Users;

namespace PinballPVP.Api.Controllers;

[ApiVersion(1)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsersController(IUserService userService) : ControllerBase
{
    // GET /api/users
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetUsers(
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var result = await userService.GetUsersAsync(page, pageSize);
        return Ok(result);
    }

    // GET /api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(int id)
    {
        var user = await userService.GetUserAsync(id);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    // POST /api/users
    [EnableRateLimiting(RateLimiterPolicyNames.AuthEndpoints)]
    [HttpPost]
    public async Task<ActionResult> CreateUser(CreateUserDto dto)
    {
        var result = await userService.CreateUserAsync(dto);

        return result.Error switch
        {
            CreateUserError.None             => CreatedAtAction(nameof(GetUser), new { id = result.User!.Id }, result.User),
            CreateUserError.UsernameInUse    => BadRequest("Username already in use"),
            CreateUserError.NicknameInUse    => BadRequest("Nickname already in use"),
            CreateUserError.EmailInUse       => BadRequest("Email already in use"),
            _                                 => BadRequest("A user with these details already exists")
        };
    }

    // PATCH /api/users/{id}/nickname
    [Authorize]
    [HttpPatch("{id}/nickname")]
    public async Task<ActionResult<UserResponseDto>> UpdateNickname(int id, UpdateNicknameDto dto)
    {
        if (User.GetUserId() != id)
            return Forbid();

        var result = await userService.UpdateNicknameAsync(id, dto);

        return result.Error switch
        {
            UpdateNicknameError.None          => Ok(result.User),
            UpdateNicknameError.UserNotFound  => NotFound(),
            _                                  => BadRequest("Nickname already in use")
        };
    }
}
