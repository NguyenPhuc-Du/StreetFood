using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace StreetFood.API.Services;

public sealed class R2StorageService
{
    private readonly IConfiguration _config;
    private readonly ILogger<R2StorageService> _logger;

    public R2StorageService(IConfiguration config, ILogger<R2StorageService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool IsEnabled =>
        string.Equals(_config["Storage:R2:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_config["Storage:R2:Bucket"])
        && !string.IsNullOrWhiteSpace(_config["Storage:R2:AccountId"])
        && !string.IsNullOrWhiteSpace(_config["Storage:R2:AccessKeyId"])
        && !string.IsNullOrWhiteSpace(_config["Storage:R2:SecretAccessKey"]);

    private string ComposeObjectKey(string objectKey)
    {
        var key = (objectKey ?? "").Trim().TrimStart('/');
        var root = (_config["Storage:R2:RootPrefix"] ?? "").Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(root)) return key;
        if (string.IsNullOrWhiteSpace(key)) return root;
        return $"{root}/{key}";
    }

    public async Task<string?> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return null;

        var accountId = _config["Storage:R2:AccountId"]!;
        var bucket = _config["Storage:R2:Bucket"]!;
        var accessKeyId = _config["Storage:R2:AccessKeyId"]!;
        var secret = _config["Storage:R2:SecretAccessKey"]!;
        var publicBase = (_config["Storage:R2:PublicBaseUrl"] ?? "").TrimEnd('/');

        if (string.IsNullOrWhiteSpace(publicBase))
        {
            _logger.LogWarning("Storage:R2:PublicBaseUrl is empty; skip R2 upload.");
            return null;
        }

        var endpoint = $"https://{accountId}.r2.cloudflarestorage.com";
        var creds = new BasicAWSCredentials(accessKeyId, secret);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            SignatureVersion = "4"
        };

        using var client = new AmazonS3Client(creds, s3Config);
        var put = new PutObjectRequest
        {
            BucketName = bucket,
            Key = ComposeObjectKey(objectKey),
            InputStream = content,
            AutoCloseStream = false,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            UseChunkEncoding = false,
            DisablePayloadSigning = true
        };
        await client.PutObjectAsync(put, cancellationToken);

        return $"{publicBase}/{put.Key}";
    }

    public async Task<bool> PutTextAsync(string objectKey, string content, string contentType = "text/plain", CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return false;
        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content ?? ""));
        var url = await UploadAsync(objectKey, ms, contentType, cancellationToken);
        return !string.IsNullOrWhiteSpace(url);
    }
}
