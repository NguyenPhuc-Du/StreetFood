using System.Text;
using System.Text.Json;

namespace StreetFood.API.Services;

public class AzureTranslatorClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AzureTranslatorClient> _logger;

    public AzureTranslatorClient(
        HttpClient http,
        IConfiguration config,
        ILogger<AzureTranslatorClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Maps DB language codes to Azure Translator "to" codes.
    /// </summary>
    public static string ToAzureCode(string dbCode) =>
        dbCode.ToLowerInvariant() switch
        {
            "cn" => "zh-Hans",
            _ => dbCode
        };

    public static string FromAzureCode(string azureCode) =>
        azureCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "cn" : azureCode[..2];

    /// <summary>
    /// Translates text into each target language. Returns dictionary keyed by DB language code (vi, en, cn, ja, ko).
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateToLanguagesAsync(
        string text,
        string sourceLangDb,
        IReadOnlyList<string> targetLangsDb,
        CancellationToken cancellationToken = default)
    {
        var key = _config["Azure:Translator:Key"];
        var region = _config["Azure:Translator:Region"] ?? "eastus";

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Azure:Translator:Key missing — returning same text for all languages.");
            var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in targetLangsDb)
                fallback[lang] = text;
            return fallback;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceAzure = ToAzureCode(sourceLangDb);
        foreach (var lang in targetLangsDb)
        {
            if (string.Equals(ToAzureCode(lang), sourceAzure, StringComparison.OrdinalIgnoreCase))
                result[lang] = text;
        }

        var apiTargets = targetLangsDb
            .Where(l => !string.Equals(ToAzureCode(l), sourceAzure, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (apiTargets.Count == 0)
            return result;

        var url = BuildTranslateUrl(sourceLangDb, apiTargets);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", key);
        req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", region);
        req.Content = new StringContent(JsonSerializer.Serialize(new[] { new { Text = text } }), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(req, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Translator API error {Status}: {Body}", response.StatusCode, json);
            throw new InvalidOperationException($"Translator API failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement[0];
        var translations = root.GetProperty("translations");

        foreach (var t in translations.EnumerateArray())
        {
            var azureTo = t.GetProperty("to").GetString() ?? "";
            var txt = t.GetProperty("text").GetString() ?? "";
            var dbCode = targetLangsDb.FirstOrDefault(
                l => string.Equals(ToAzureCode(l), azureTo, StringComparison.OrdinalIgnoreCase));
            if (dbCode != null)
                result[dbCode] = txt;
        }

        foreach (var lang in targetLangsDb)
        {
            if (!result.ContainsKey(lang))
                result[lang] = text;
        }

        return result;
    }

    private static string BuildTranslateUrl(string sourceLangDb, IReadOnlyList<string> targetLangsDb)
    {
        var from = ToAzureCode(sourceLangDb);
        var toParams = string.Join("&", targetLangsDb.Select(l => $"to={Uri.EscapeDataString(ToAzureCode(l))}"));
        return $"https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from={Uri.EscapeDataString(from)}&{toParams}";
    }
}
