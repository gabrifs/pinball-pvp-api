using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record UpdateNicknameDto(
    [Required][StringLength(50, MinimumLength = 1)] string Nickname
);
