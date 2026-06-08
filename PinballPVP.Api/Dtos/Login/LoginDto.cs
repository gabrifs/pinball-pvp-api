using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record LoginDto(
    [Required] string Username,
    [Required] string Password
);