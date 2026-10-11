using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Reads secrets from Scaleway Secret Manager via REST API.
/// Uses the <c>secrets-by-path</c> endpoint to access secrets by folder path and name.
/// </summary>
/// <remarks>
/// Vault paths use "/" as separator. The internal dotted path (e.g. "tenantDB.postgresql.dev")
/// is converted to "tenantDB/postgresql/dev" by the tenant connection factory.
/// The last segment becomes the secret name, everything before is the secret path.
/// </remarks>
public sealed partial class ScalewaySecretsProvider : ISecretsProvider
{
    internal const string HttpClientName = "Stellaegis.ScalewaySecrets";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ScalewaySecretsOptions _options;
    private readonly ILogger<ScalewaySecretsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ScalewaySecretsProvider"/>.
    /// </summary>
    public ScalewaySecretsProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ScalewaySecretsOptions> options,
        ILogger<ScalewaySecretsProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(opts.ProjectId))
            throw new ArgumentException("ProjectId is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(opts.ApiToken))
            throw new ArgumentException("ApiToken is required.", nameof(options));

        _options = opts;
    }

    /// <inheritdoc />
    /// <remarks>Uses "/" — Scaleway Secret Manager organizes secrets in folder paths.</remarks>
    public string Separator => "/";

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Split vault path into folder (secret_path) and leaf (secret_name).
        // "tenantDB/postgresql/dev" → path="/tenantDB/postgresql", name="dev"
        var lastSep = path.LastIndexOf('/');
        string secretPath, secretName;
        if (lastSep >= 0)
        {
            secretPath = path[..lastSep];
            secretName = path[(lastSep + 1)..];
            if (string.IsNullOrEmpty(secretPath)) secretPath = "/";
        }
        else
        {
            secretPath = "/";
            secretName = path;
        }

        var url = $"{_options.BaseUrl}/secret-manager/v1beta1/regions/{Uri.EscapeDataString(_options.Region)}" +
                  $"/secrets-by-path/versions/latest/access" +
                  $"?secret_name={Uri.EscapeDataString(secretName)}" +
                  $"&secret_path={Uri.EscapeDataString(secretPath)}" +
                  $"&project_id={Uri.EscapeDataString(_options.ProjectId)}";

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Auth-Token", _options.ApiToken);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError("Secret not found in Scaleway for path '{Path}'", path);
            throw new KeyNotFoundException($"Secret not found: {path}");
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync(
            ScalewayJsonContext.Default.ScalewayAccessResponse,
            cancellationToken).ConfigureAwait(false);

        if (json?.Data is null)
            throw new InvalidOperationException($"Empty response from Scaleway for path '{path}'.");

        _logger.LogDebug("Retrieved secret from Scaleway for path '{Path}'", path);
        return Encoding.UTF8.GetString(Convert.FromBase64String(json.Data));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                result[path] = await GetSecretAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning("Secret not found during batch retrieval: '{Path}'", path);
            }
        }

        return result;
    }

    internal sealed record ScalewayAccessResponse(
        [property: JsonPropertyName("data")] string? Data);

    [JsonSerializable(typeof(ScalewayAccessResponse))]
    internal sealed partial class ScalewayJsonContext : JsonSerializerContext;
}
