namespace BigSchool.Application.SharedKernel.Common;

public record ApiResponse<T>
{
    public T? Data { get; init; }
    public List<ApiError> Errors { get; init; } = [];
    public MetaData? Meta { get; init; }

    public static ApiResponse<T> Success(T data, MetaData? meta = null)
        => new() { Data = data, Meta = meta };

    public static ApiResponse<T> Fail(params ApiError[] errors)
        => new() { Errors = [.. errors] };
}

public record ApiResponse
{
    public object? Data { get; init; }
    public List<ApiError> Errors { get; init; } = [];
    public MetaData? Meta { get; init; }

    public static ApiResponse Success(object? data = null, MetaData? meta = null)
        => new() { Data = data, Meta = meta };

    public static ApiResponse Fail(params ApiError[] errors)
        => new() { Errors = [.. errors] };
}

public record MetaData
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? TotalCount { get; init; }
    public int? TotalPages => TotalCount.HasValue && PageSize.HasValue && PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount.Value / PageSize.Value)
        : null;
}
