namespace BigSchool.Application.SharedKernel.Common;

/// <summary>
/// Error tipado siguiendo RFC 7807 Problem Details.
/// El frontend puede hacer switch sobre Code para gestionar cada caso.
/// </summary>
public record ApiError
{
    /// <summary>Código de error para switch/case en frontend (ej: "INVALID_CREDENTIALS", "VALIDATION_ERROR")</summary>
    public required string Code { get; init; }
    
    /// <summary>Mensaje legible para el usuario</summary>
    public required string Message { get; init; }
    
    /// <summary>Campo al que se refiere el error (null si es general)</summary>
    public string? Field { get; init; }
}
