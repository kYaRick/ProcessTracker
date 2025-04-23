using ConfigRunner.Constants;
using System.Text.Json;

namespace ConfigRunner.Utilities;

/// <summary>
/// Utilities for JSON serialization and deserialization
/// </summary>
public static class JsonSerializerUtilities
{
   /// <summary>
   /// Default JSON serialization options
   /// </summary>
   internal static readonly JsonSerializerOptions _defaultOptions = new()
   {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
   };

   /// <summary>
   /// Gets the default JSON serializer options
   /// </summary>
   public static JsonSerializerOptions DefaultOptions =>
      _defaultOptions;

   /// <summary>
   /// Serializes an object to JSON
   /// </summary>
   /// <typeparam name="T">Type of object to serialize</typeparam>
   /// <param name="value">Object to serialize</param>
   /// <returns>JSON string</returns>
   internal static string Serialize<T>(T value) where T : class =>
      JsonSerializer.Serialize(value, _defaultOptions);

   /// <summary>
   /// Deserializes a JSON string to an object
   /// </summary>
   /// <typeparam name="T">Type to deserialize to</typeparam>
   /// <param name="json">JSON string</param>
   /// <returns>Deserialized object</returns>
   internal static T? Deserialize<T>(string json) where T : class, new() =>
      string.IsNullOrWhiteSpace(json) || json.Equals(ConfigurationConstants.EMPTY_CONFIG)
          ? new T()
          : JsonSerializer.Deserialize<T>(json, _defaultOptions);
}
