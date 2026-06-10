using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record CreateUserDto(
    [Required][StringLength(50)] string Username,
    [Required][StringLength(50)] string Nickname,
    [Required][StringLength(255)][EmailAddress] string Email,
    [Required][StringLength(256, MinimumLength = 8)] string Password
);
