using BigSchool.Application.Auth.Interfaces.Services;
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.Notifications.Queries.GetEmailById;
using BigSchool.Application.Notifications.Queries.GetEmails;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Notifications;

[ApiController]
[Authorize]
[Route("api/v1/emails")]
public class EmailsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public EmailsController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmailLogListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetEmailsQuery(UserId, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<EmailLogListItemDto>>.Success(result.Items, meta));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<EmailLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetEmailByIdQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Email no encontrado." }))
            : Ok(ApiResponse<EmailLogDto>.Success(result));
    }
}
