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
/// The last segment of a <see cref="SecretPath"/> is the secret's name, the segments before it its
/// folder — see <see cref="ScalewaySecretNameFormat"/>.
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

    /// <summary>The native names of this provider: folders, then the secret's name.</summary>
    public static ISecretNameFormat NameFormat => ScalewaySecretNameFormat.Instance;

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(SecretPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var (secretPath, secretName) = ScalewaySecretNameFormat.FolderAndName(path);

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
    public async Task<IReadOnlyDictionary<SecretPath, string>> GetSecretsAsync(
        IEnumerable<SecretPath> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new Dictionary<SecretPath, string>();
        foreach (var path in paths)
        {
            if (path is null) continue;
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
