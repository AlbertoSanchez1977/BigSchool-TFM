using BigSchool.Application.Investments.Commands.AddHolding;
using BigSchool.Application.Investments.Commands.CreatePortfolio;
using BigSchool.Application.Investments.Commands.DeleteHolding;
using BigSchool.Application.Investments.Commands.SellShares;
using BigSchool.Application.Investments.Commands.UpdateHolding;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Investments.DTOs;
using BigSchool.Application.Investments.Queries.GetPortfolioById;
using BigSchool.Application.Investments.Queries.GetPortfolioPerformance;
using BigSchool.Application.Investments.Queries.GetPortfolios;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.WebApi.Controllers.Investments;

[ApiController]
[Authorize]
[Route("api/v1/portfolios")]
public class PortfoliosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public PortfoliosController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    public record CreatePortfolioRequest(string Name);
    public record AddHoldingRequest(int IdCompany, decimal Shares, decimal BuyPrice, DateOnly BuyDate, string? Notes);
    public record UpdateHoldingRequest(string? Notes);
    public record SellSharesRequest(int CompanyId, decimal Shares, decimal SellPrice, DateOnly SellDate, string? Notes);

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PortfolioListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetPortfoliosQuery(UserId));
        return Ok(ApiResponse<IReadOnlyList<PortfolioListItemDto>>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PortfolioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioRequest body)
    {
        var result = await _mediator.Send(new CreatePortfolioCommand(UserId, body.Name));
        return Ok(ApiResponse<PortfolioDto>.Success(result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PortfolioDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetPortfolioByIdQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Cartera no encontrada." }))
            : Ok(ApiResponse<PortfolioDetailDto>.Success(result));
    }

    [HttpGet("{id:int}/performance")]
    [ProducesResponseType(typeof(ApiResponse<PortfolioPerformanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Performance(int id)
    {
        var result = await _mediator.Send(new GetPortfolioPerformanceQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Cartera no encontrada." }))
            : Ok(ApiResponse<PortfolioPerformanceDto>.Success(result));
    }

    [HttpPost("{id:int}/holdings")]
    [ProducesResponseType(typeof(ApiResponse<HoldingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddHolding(int id, [FromBody] AddHoldingRequest body)
    {
        var command = new AddHoldingCommand(id, UserId, body.IdCompany, body.Shares, body.BuyPrice, body.BuyDate, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<HoldingDto>.Success(result));
    }

    [HttpPut("{id:int}/holdings/{holdingId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HoldingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHolding(int id, int holdingId, [FromBody] UpdateHoldingRequest body)
    {
        var command = new UpdateHoldingCommand(id, UserId, holdingId, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<HoldingDto>.Success(result));
    }

    [HttpDelete("{id:int}/holdings/{holdingId:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHolding(int id, int holdingId)
    {
        await _mediator.Send(new DeleteHoldingCommand(id, UserId, holdingId));
        return Ok(ApiResponse.Success());
    }

    [HttpPost("{id:int}/sales")]
    [ProducesResponseType(typeof(ApiResponse<SellSharesResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sell(int id, [FromBody] SellSharesRequest body)
    {
        var command = new SellSharesCommand(id, UserId, body.CompanyId, body.Shares, body.SellPrice, body.SellDate, body.Notes);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<SellSharesResultDto>.Success(result));
    }
}
