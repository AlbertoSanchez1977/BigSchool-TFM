using BigSchool.Application.Notifications.Commands.CreateContact;
using BigSchool.Application.Notifications.DTOs;
using BigSchool.Application.Notifications.Queries.GetContacts;
using BigSchool.Application.SharedKernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Notifications;

[ApiController]
[Route("api/v1/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactsController(IMediator mediator) => _mediator = mediator;

    public record CreateContactRequest(string FullName, string Email, string Message);

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest body)
    {
        var id = await _mediator.Send(new CreateContactCommand(body.FullName, body.Email, body.Message));
        return Ok(ApiResponse<object>.Success(new { idContact = id }));
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ContactListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetContactsQuery(page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<ContactListItemDto>>.Success(result.Items, meta));
    }
}
