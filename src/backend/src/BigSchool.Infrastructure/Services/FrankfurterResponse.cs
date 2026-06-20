using System.Text.Json.Serialization;

namespace BigSchool.Infrastructure.Services;

/// <summary>DTO de la respuesta de Frankfurter: { "amount":1.0, "base":"USD", "date":"2026-06-17", "rates":{"EUR":0.92} }.</summary>
public sealed class FrankfurterResponse
{
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("base")] public string Base { get; set; } = string.Empty;
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("rates")] public Dictionary<string, decimal> Rates { get; set; } = new();
}
