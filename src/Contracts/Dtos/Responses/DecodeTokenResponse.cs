using System.Text.Json.Serialization;
using Contracts.Constants;

namespace Contracts.Dtos.Responses;

public class DecodeTokenResponse
{
    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    [JsonPropertyName(ClaimTypes.TokenFamilyId)]
    public string? FamilyId { get; set; }
}