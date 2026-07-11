using BigSchool.Application.Auth.Commands.UpdateUser;
using BigSchool.Application.Auth.DTOs;
using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Application.Auth.Queries.GetMe;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Auth;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public UsersController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me()
    {
        var result = await _mediator.Send(new GetMeQuery(UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Usuario no encontrado." }))
            : Ok(ApiResponse<UserProfileDto>.Success(result));
    }

    public record UpdateUserRequest(string FullName, string? Password);

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest body)
    {
        var result = await _mediator.Send(new UpdateUserCommand(UserId, body.FullName, body.Password));
        return Ok(ApiResponse<UserProfileDto>.Success(result));
    }
}
