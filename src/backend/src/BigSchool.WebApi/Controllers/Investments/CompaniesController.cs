using BigSchool.Application.Investments.Commands.AddValuation;
using BigSchool.Application.Investments.Commands.CreateCompany;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries.GetCompanies;
using BigSchool.Application.Investments.Queries.GetCompanyById;
using BigSchool.Application.Investments.Queries.GetCompanyValuations;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers.Investments;

[ApiController]
[Authorize]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    public record CreateCompanyRequest(string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency);
    public record AddValuationRequest(decimal Price, DateOnly Date, string? Source);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] Sector? sector, [FromQuery] Market? market,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCompaniesQuery(sector, market, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<CompanyListItemDto>>.Success(result.Items, meta));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetCompanyByIdQuery(id));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Empresa no encontrada." }))
            : Ok(ApiResponse<CompanyListItemDto>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyRequest body)
    {
        var command = new CreateCompanyCommand(body.Name, body.Ticker, body.Sector, body.Market, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<CompanyDto>.Success(result));
    }

    [HttpGet("{id:int}/valuations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ValuationListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuations(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetCompanyValuationsQuery(id, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<ValuationListItemDto>>.Success(result.Items, meta));
    }

    [HttpPost("{id:int}/valuations")]
    [ProducesResponseType(typeof(ApiResponse<ValuationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddValuation(int id, [FromBody] AddValuationRequest body)
    {
        var command = new AddValuationCommand(id, body.Price, body.Date, body.Source);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<ValuationDto>.Success(result));
    }
}
