using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MechabellumModManager.Models;

namespace MechabellumModManager.Tests.Support;

/// <summary>Signs takedown lists the way tools/sign-removed.ps1 does, with a throwaway P-256 key.</summary>
public sealed class TestRemovalSigner : IDisposable
{
    readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKey
    {
        get
        {
            var q = _key.ExportParameters(includePrivateParameters: false).Q;
            return Convert.ToBase64String([.. q.X!, .. q.Y!]);
        }
    }

    public CatalogSignedBlob Sign(IEnumerable<CatalogRemoval> removed, DateTimeOffset? issuedAt = null) =>
        SignPayload(PayloadJson(removed, issuedAt ?? DateTimeOffset.UtcNow));

    public CatalogSignedBlob SignPayload(string payloadJson)
    {
        var bytes = Encoding.UTF8.GetBytes(payloadJson);
        return new CatalogSignedBlob
        {
            Payload = Convert.ToBase64String(bytes),
            Signature = Convert.ToBase64String(_key.SignData(bytes, HashAlgorithmName.SHA256))
        };
    }

    public static string PayloadJson(IEnumerable<CatalogRemoval> removed, DateTimeOffset issuedAt) =>
        JsonSerializer.Serialize(new
        {
            issuedAt = issuedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            removed
        });

    public void Dispose() => _key.Dispose();
}
