using BigSchool.Application.Finance.Commands.Create;
using BigSchool.Application.Finance.Commands.Delete;
using BigSchool.Application.Finance.Commands.Update;
using BigSchool.Application.SharedKernel.Common;
using BigSchool.Application.Finance.DTOs;
using BigSchool.Application.Finance.Queries.Transactions.GetByCategory;
using BigSchool.Application.Finance.Queries.Transactions.GetMonthly;
using BigSchool.Application.Finance.Queries.Transactions.GetMonthlyChart;
using BigSchool.Application.Finance.Queries.Transactions.GetTransactionById;
using BigSchool.Application.Finance.Queries.Transactions.GetTransactions;
using BigSchool.Application.Finance.Queries.Transactions.GetTransactionSummary;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BigSchool.Application.Auth.Interfaces.Services;

namespace BigSchool.WebApi.Controllers.Finance;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserIdEncryptor _encryptor;

    public TransactionsController(IMediator mediator, IUserIdEncryptor encryptor)
    {
        _mediator = mediator;
        _encryptor = encryptor;
    }

    private int UserId => CurrentUser.GetId(User, _encryptor);

    public record CreateTransactionRequest(
        TransactionType Type, MainCategory IdMainCategory, int? IdSubCategory,
        string? Description, DateOnly TransactionDate, decimal Amount, Currency? Currency);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest body)
    {
        var command = new CreateTransactionCommand(UserId, body.Type, body.IdMainCategory,
            body.IdSubCategory, body.Description, body.TransactionDate, body.Amount, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<TransactionDto>.Success(result));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTransactionRequest body)
    {
        var command = new UpdateTransactionCommand(id, UserId, body.Type, body.IdMainCategory,
            body.IdSubCategory, body.Description, body.TransactionDate, body.Amount, body.Currency);
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<TransactionDto>.Success(result));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteTransactionCommand(id, UserId));
        return Ok(ApiResponse.Success());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetTransactionByIdQuery(id, UserId));
        return result is null
            ? NotFound(ApiResponse.Fail(new ApiError { Code = "ENTITY_NOT_FOUND", Message = "Transacción no encontrada." }))
            : Ok(ApiResponse<TransactionListItemDto>.Success(result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TransactionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] TransactionType? type, [FromQuery] MainCategory? category,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTransactionsQuery(UserId, type, category, from, to, page, pageSize));
        var meta = new MetaData { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount };
        return Ok(ApiResponse<IReadOnlyList<TransactionListItemDto>>.Success(result.Items, meta));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<TransactionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var result = await _mediator.Send(new GetTransactionSummaryQuery(UserId, from, to));
        return Ok(ApiResponse<TransactionSummaryDto>.Success(result));
    }

    [HttpGet("monthly-chart")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyChart([FromQuery] int year)
    {
        var result = await _mediator.Send(new GetMonthlyChartQuery(UserId, year));
        return Ok(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>.Success(result));
    }

    [HttpGet("by-category")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryTotalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ByCategory([FromQuery] TransactionType? type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var result = await _mediator.Send(new GetTransactionsByCategoryQuery(UserId, type, from, to));
        return Ok(ApiResponse<IReadOnlyList<CategoryTotalDto>>.Success(result));
    }

    [HttpGet("monthly")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Monthly([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] MainCategory? category, [FromQuery] TransactionType? type)
    {
        var result = await _mediator.Send(new GetMonthlyQuery(UserId, from, to, category, type));
        return Ok(ApiResponse<IReadOnlyList<MonthlyChartPointDto>>.Success(result));
    }
}
