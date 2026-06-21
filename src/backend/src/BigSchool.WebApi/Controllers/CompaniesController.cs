using BigSchool.Application.Commands.Investments.AddValuation;
using BigSchool.Application.Commands.Investments.CreateCompany;
using BigSchool.Application.Common;
using BigSchool.Application.DTOs.Investments;
using BigSchool.Application.Queries.Investments.GetCompanies;
using BigSchool.Application.Queries.Investments.GetCompanyById;
using BigSchool.Application.Queries.Investments.GetCompanyValuations;
using BigSchool.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BigSchool.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/companies")]
public class CompaniesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompaniesController(IMediator mediator) => _mediator = mediator;

    public record CreateCompanyRequest(string Name, string Ticker, string? Sector, string? Market, Currency Currency);
    public record AddValuationRequest(decimal Price, DateOnly Date, string? Source);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CompanyListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string? sector, [FromQuery] string? market)
    {
        var result = await _mediator.Send(new GetCompaniesQuery(sector, market));
        return Ok(ApiResponse<IReadOnlyList<CompanyListItemDto>>.Success(result));
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
    public async Task<IActionResult> GetValuations(int id)
    {
        var result = await _mediator.Send(new GetCompanyValuationsQuery(id));
        return Ok(ApiResponse<IReadOnlyList<ValuationListItemDto>>.Success(result));
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
