using System.ComponentModel.DataAnnotations;

namespace PinballPVP.Api.Dtos;

public record ResetPasswordDto(
    [Required] int UserId,
    [Required] string RecoveryCode,
    [Required][StringLength(256, MinimumLength = 8)] string NewPassword
);
