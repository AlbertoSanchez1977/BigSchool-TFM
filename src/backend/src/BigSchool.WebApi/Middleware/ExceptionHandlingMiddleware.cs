using System.Net;
using System.Text.Json;
using BigSchool.Application.Common;
using BigSchool.Domain.Exceptions;
using FluentValidation;

namespace BigSchool.WebApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                validationEx.Errors.Select(e => new ApiError
                {
                    Code = "VALIDATION_ERROR",
                    Message = e.ErrorMessage,
                    Field = e.PropertyName
                }).ToArray()
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new[] { new ApiError { Code = notFoundEx.ErrorCode, Message = notFoundEx.Message } }
            ),
            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                new[] { new ApiError { Code = conflictEx.ErrorCode, Message = conflictEx.Message } }
            ),
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                new[] { new ApiError { Code = domainEx.ErrorCode, Message = domainEx.Message } }
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new[] { new ApiError { Code = "UNAUTHORIZED", Message = "No autorizado." } }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new[] { new ApiError { Code = "INTERNAL_ERROR", Message = "Error interno del servidor." } }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception: {ErrorCode}", errors.FirstOrDefault()?.Code);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse.Fail(errors);
        var json = JsonSerializer.Serialize(response, JsonOptions);

        await context.Response.WriteAsync(json);
    }
}
