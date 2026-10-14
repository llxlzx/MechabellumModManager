using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MechabellumModManager.Models;

namespace MechabellumModManager.Services;

public sealed record VerifiedRemovalList(DateTimeOffset IssuedAt, IReadOnlyList<CatalogRemoval> Removed);

/// <summary>
/// Checks the maintainer signature on a takedown list. Deleting files on players' machines is the
/// one catalog action a compromised mirror must not be able to trigger, so the list is signed
/// offline with ECDSA P-256 / SHA-256 (tools/sign-removed.ps1) and verified here against a key
/// compiled into the manager. With no key compiled in, no list is ever accepted.
/// </summary>
public static class RemovalListVerifier
{
    /// <summary>
    /// Maintainer public key: base64 of the P-256 point X||Y (64 bytes), as printed by
    /// <c>tools/sign-removed.ps1 -NewKey</c>. Empty disables takedowns.
    /// </summary>
    public const string PublicKey = "sBj+xkfDBykYEzgc6uEQyha8ucUq7jLbQC/Y7fd1wqYurDgrn197qriMPYr5O2qo1Iza/63zoyivokhHzFxMgQ==";

    public const int MaxPayloadBytes = 1_048_576;

    public const string ErrorNoKey = "no-key";
    public const string ErrorMissing = "missing";
    public const string ErrorEncoding = "encoding";
    public const string ErrorSignature = "signature";
    public const string ErrorPayload = "payload";

    public const string StoreFileName = "removal-list.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    sealed class Payload
    {
        [JsonPropertyName("issuedAt")]
        public string? IssuedAt { get; set; }

        [JsonPropertyName("removed")]
        public List<CatalogRemoval>? Removed { get; set; }
    }

    public static bool TryVerify(
        CatalogSignedBlob? blob,
        string? publicKey,
        [NotNullWhen(true)] out VerifiedRemovalList? list,
        out string error)
    {
        list = null;
        if (!TryImportKey(publicKey, out var key))
        {
            error = ErrorNoKey;
            return false;
        }

        using (key)
        {
            if (blob is null || string.IsNullOrWhiteSpace(blob.Payload) || string.IsNullOrWhiteSpace(blob.Signature))
            {
                error = ErrorMissing;
                return false;
            }

            byte[] payload;
            byte[] signature;
            try
            {
                if (blob.Payload.Length > MaxPayloadBytes / 3 * 4 + 4)
                {
                    error = ErrorEncoding;
                    return false;
                }

                payload = Convert.FromBase64String(blob.Payload.Trim());
                signature = Convert.FromBase64String(blob.Signature.Trim());
            }
            catch (FormatException)
            {
                error = ErrorEncoding;
                return false;
            }

            if (payload.Length > MaxPayloadBytes || signature.Length != 64)
            {
                error = payload.Length > MaxPayloadBytes ? ErrorEncoding : ErrorSignature;
                return false;
            }

            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256))
            {
                error = ErrorSignature;
                return false;
            }

            Payload? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<Payload>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                error = ErrorPayload;
                return false;
            }

            if (parsed?.Removed is null ||
                !DateTimeOffset.TryParse(
                    parsed.IssuedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var issuedAt))
            {
                error = ErrorPayload;
                return false;
            }

            list = new VerifiedRemovalList(issuedAt, parsed.Removed.Where(r => r is not null).ToList());
            error = "";
            return true;
        }
    }

    /// <summary>The last accepted list, kept apart from the catalog cache so an upgrade or an older mirror copy cannot roll it back.</summary>
    public static bool TryLoadAccepted(
        string? dataRoot,
        string? publicKey,
        [NotNullWhen(true)] out VerifiedRemovalList? list,
        out CatalogSignedBlob? blob)
    {
        list = null;
        blob = null;
        if (string.IsNullOrWhiteSpace(dataRoot))
            return false;
        var path = Path.Combine(dataRoot, StoreFileName);
        if (!File.Exists(path))
            return false;
        try
        {
            blob = JsonSerializer.Deserialize<CatalogSignedBlob>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return false;
        }

        return TryVerify(blob, publicKey, out list, out _);
    }

    public static void SaveAccepted(string? dataRoot, CatalogSignedBlob blob)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            return;
        Directory.CreateDirectory(dataRoot);
        var path = Path.Combine(dataRoot, StoreFileName);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(blob, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }

    static bool TryImportKey(string? publicKey, [NotNullWhen(true)] out ECDsa? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(publicKey))
            return false;

        byte[] point;
        try
        {
            point = Convert.FromBase64String(publicKey.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        if (point.Length != 64)
            return false;

        try
        {
            key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[..32], Y = point[32..] }
            });
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or ArgumentException)
        {
            // A mis-pasted key must disable takedowns, not break every catalog refresh.
            key?.Dispose();
            key = null;
            return false;
        }
    }
}
