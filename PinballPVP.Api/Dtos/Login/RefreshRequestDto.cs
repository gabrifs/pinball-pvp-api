using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record RefreshRequestDto(
    [Required] string RefreshToken
);
