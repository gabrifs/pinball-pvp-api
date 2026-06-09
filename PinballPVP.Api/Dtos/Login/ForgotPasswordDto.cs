using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record ForgotPasswordDto(
    [Required] int UserId
);
