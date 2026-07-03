namespace BigSchool.Application.SharedKernel.Common;

/// <summary>Política única de paginación (dedup de la que vivía en GetTransactionsQuery).</summary>
public static class Pagination
{
    public const int DEFAULT_PAGE_SIZE = 20;
    public const int MAX_PAGE_SIZE = 100;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) => pageSize switch
    {
        <= 0 => DEFAULT_PAGE_SIZE,
        > MAX_PAGE_SIZE => MAX_PAGE_SIZE,
        _ => pageSize
    };
}
