using FluentAssertions;
using MechabellumModManager.Models;
using MechabellumModManager.Services;
using MechabellumModManager.Tests.Support;

namespace MechabellumModManager.Tests;

public class RemovalListVerifierTests
{
    static readonly CatalogRemoval Econ = new() { Id = "opponent-economy", Reason = "unfair" };

    [Fact]
    public void A_list_signed_with_the_maintainer_key_is_accepted()
    {
        using var signer = new TestRemovalSigner();
        var issued = new DateTimeOffset(2026, 10, 14, 1, 2, 3, TimeSpan.Zero);

        RemovalListVerifier.TryVerify(signer.Sign([Econ], issued), signer.PublicKey, out var list, out var error)
            .Should().BeTrue(error);

        list!.IssuedAt.Should().Be(issued);
        list.Removed.Should().ContainSingle(r => r.Id == "opponent-economy" && r.Reason == "unfair");
    }

    [Fact]
    public void An_edited_payload_is_rejected()
    {
        using var signer = new TestRemovalSigner();
        var blob = signer.Sign([Econ]);
        var forged = signer.SignPayload(TestRemovalSigner.PayloadJson(
            [Econ, new CatalogRemoval { Id = "show-grid" }], DateTimeOffset.UtcNow));
        blob.Payload = forged.Payload;

        RemovalListVerifier.TryVerify(blob, signer.PublicKey, out _, out var error).Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorSignature);
    }

    [Fact]
    public void A_list_signed_by_another_key_is_rejected()
    {
        using var maintainer = new TestRemovalSigner();
        using var attacker = new TestRemovalSigner();

        RemovalListVerifier.TryVerify(attacker.Sign([Econ]), maintainer.PublicKey, out _, out var error)
            .Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorSignature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AAAA")]
    public void Without_a_usable_public_key_nothing_is_accepted(string? publicKey)
    {
        using var signer = new TestRemovalSigner();

        RemovalListVerifier.TryVerify(signer.Sign([Econ]), publicKey, out _, out var error).Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorNoKey);
    }

    [Fact]
    public void A_64_byte_key_that_is_not_a_curve_point_is_treated_as_no_key()
    {
        using var signer = new TestRemovalSigner();
        var offCurve = Convert.ToBase64String(new byte[64]);

        RemovalListVerifier.TryVerify(signer.Sign([Econ]), offCurve, out _, out var error).Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorNoKey);
    }

    [Fact]
    public void Missing_or_malformed_blobs_are_rejected()
    {
        using var signer = new TestRemovalSigner();

        RemovalListVerifier.TryVerify(null, signer.PublicKey, out _, out var missing).Should().BeFalse();
        missing.Should().Be(RemovalListVerifier.ErrorMissing);

        RemovalListVerifier.TryVerify(
                new CatalogSignedBlob { Payload = "%%%", Signature = "%%%" }, signer.PublicKey, out _, out var encoding)
            .Should().BeFalse();
        encoding.Should().Be(RemovalListVerifier.ErrorEncoding);
    }

    [Fact]
    public void A_signed_payload_without_issued_at_is_rejected()
    {
        using var signer = new TestRemovalSigner();

        RemovalListVerifier.TryVerify(signer.SignPayload("""{"removed":[]}"""), signer.PublicKey, out _, out var error)
            .Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorPayload);
    }

    [Fact]
    public void An_oversized_payload_is_rejected_before_verifying()
    {
        using var signer = new TestRemovalSigner();
        var huge = new CatalogRemoval { Id = "x", Reason = new string('a', RemovalListVerifier.MaxPayloadBytes) };

        RemovalListVerifier.TryVerify(signer.Sign([huge]), signer.PublicKey, out _, out var error).Should().BeFalse();
        error.Should().Be(RemovalListVerifier.ErrorEncoding);
    }

    [Fact]
    public void The_shipped_key_is_either_empty_or_a_valid_point()
    {
        if (RemovalListVerifier.PublicKey.Length == 0)
            return;
        Convert.FromBase64String(RemovalListVerifier.PublicKey).Should().HaveCount(64);
    }
}
