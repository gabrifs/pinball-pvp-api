using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Dtos;

public record UserResponseDto(
    int Id,
    string Nickname
)
{
    public static readonly Expression<Func<User, UserResponseDto>>
        Projection =
            user => new UserResponseDto
            (
                user.Id,
                user.Nickname
            );

    public static UserResponseDto FromEntity(User user)
    {
        return new UserResponseDto
            (
                user.Id,
                user.Nickname
            );
    }
}
