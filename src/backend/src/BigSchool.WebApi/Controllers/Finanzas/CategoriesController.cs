using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Finanzas.Queries.Categories.GetCategories;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.WebApi.Controllers.Finanzas;

[ApiController]
[Authorize]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public CategoriesController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        var result = await _mediator.Send(new GetCategoriesQuery(userId));
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Success(result));
    }
}
