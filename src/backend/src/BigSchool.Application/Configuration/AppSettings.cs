namespace BigSchool.Application.Configuration;

public class AppSettings
{
    public const string SectionName = "AppSettings";

    public required string ConnectionString { get; set; }

    public required JwtSettings Jwt { get; set; }

    public required RagServiceSettings RagService { get; set; }

    public required ExchangeRateSettings ExchangeRate { get; set; }
}

public class JwtSettings
{
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpirationMinutes { get; set; } = 60;
}

public class RagServiceSettings
{
    public required string BaseUrl { get; set; }
}

public class ExchangeRateSettings
{
    public required string BaseUrl { get; set; }
}
