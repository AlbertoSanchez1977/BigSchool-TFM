using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Finanzas.Commands.CreateSubCategory;
using BigSchool.Application.Finanzas.Commands.DeleteSubCategory;
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

    public record CreateSubCategoryRequest(BigSchool.Domain.Finanzas.Enums.MainCategory MainCategory, string Name);

    [HttpPost("sub")]
    [ProducesResponseType(typeof(ApiResponse<BigSchool.Application.Finanzas.DTOs.SubCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSub([FromBody] CreateSubCategoryRequest body)
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        var result = await _mediator.Send(new CreateSubCategoryCommand(userId, body.MainCategory, body.Name));
        return Ok(ApiResponse<BigSchool.Application.Finanzas.DTOs.SubCategoryDto>.Success(result));
    }

    [HttpDelete("sub/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSub(int id)
    {
        var userId = CurrentUser.GetId(User, _encryptor);
        await _mediator.Send(new DeleteSubCategoryCommand(userId, id));
        return Ok(ApiResponse.Success());
    }
}
