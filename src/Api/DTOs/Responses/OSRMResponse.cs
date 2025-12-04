using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TourAppBackend.src.Api.DTOs.Responses
{
 public class OSRMResponse
 {
 [JsonPropertyName("routes")]
 public List<Route> Routes { get; set; }

 public class Route
 {
 [JsonPropertyName("distance")]
 public double Distance { get; set; }

 [JsonPropertyName("duration")]
 public double Duration { get; set; }

 [JsonPropertyName("geometry")]
 public string Geometry { get; set; }
 }
 }
}