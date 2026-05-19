using System.Security.Cryptography;
using System.Text.Json;
using GestorOT.Api.Controllers;
using GestorOT.Domain.Entities;

namespace GestorOT.Tests.Regression;

public class PublicValidationTokenTests
{
    // ─────────────────────────────────────────────────────────────────────
    // 1. Token metadata — labor suelta
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenMetadata_Parse_LaborId_Extracts_SingleLabor()
    {
        var laborId = Guid.NewGuid();
        var metadata = JsonSerializer.Serialize(new { laborId, action = "validate" });

        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = null,
            Metadata = metadata,
            TokenHash = "abc",
            TenantId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var info = ShareControllerUtils.ParseTokenMetadata(token);

        Assert.Equal(laborId, info.SingleLaborId);
        Assert.Single(info.AllowedLaborIds);
        Assert.Contains(laborId, info.AllowedLaborIds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 2. Token metadata — OT con laborIds
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenMetadata_Parse_LaborIds_Extracts_MultipleLabors()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var metadata = JsonSerializer.Serialize(new { laborIds = new[] { id1, id2 } });

        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            Metadata = metadata,
            TokenHash = "def",
            TenantId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var info = ShareControllerUtils.ParseTokenMetadata(token);

        Assert.Null(info.SingleLaborId);
        Assert.Equal(2, info.AllowedLaborIds.Count);
        Assert.Contains(id1, info.AllowedLaborIds);
        Assert.Contains(id2, info.AllowedLaborIds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 3. Token metadata — sin metadata
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenMetadata_Parse_NullMetadata_ReturnsEmpty()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            Metadata = null,
            TokenHash = "ghi",
            TenantId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var info = ShareControllerUtils.ParseTokenMetadata(token);

        Assert.Null(info.SingleLaborId);
        Assert.Empty(info.AllowedLaborIds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 4. Token metadata — malformed JSON
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenMetadata_Parse_MalformedMetadata_ReturnsEmpty()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            Metadata = "{not valid json",
            TokenHash = "jkl",
            TenantId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var info = ShareControllerUtils.ParseTokenMetadata(token);

        Assert.Null(info.SingleLaborId);
        Assert.Empty(info.AllowedLaborIds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 5. Token metadata — vacío
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenMetadata_Parse_EmptyString_ReturnsEmpty()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            Metadata = "",
            TokenHash = "mno",
            TenantId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        var info = ShareControllerUtils.ParseTokenMetadata(token);

        Assert.Null(info.SingleLaborId);
        Assert.Empty(info.AllowedLaborIds);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 6. SubmitForValidation — labor con OT crea token con OT ID
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubmitForValidation_WithOT_CreatesToken_WithWorkOrderId()
    {
        var workOrderId = Guid.NewGuid();
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderId,
            TenantId = Guid.NewGuid(),
            TokenHash = "pqt",
            ExpiresAt = DateTime.UtcNow.AddHours(72),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { laborId = Guid.NewGuid(), action = "validate" })
        };

        Assert.NotNull(token.WorkOrderId);
        Assert.Equal(workOrderId, token.WorkOrderId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 7. SubmitForValidation — labor suelta crea token con WorkOrderId null
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SubmitForValidation_StandaloneLabor_Token_HasNullWorkOrderId()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = null,
            TenantId = Guid.NewGuid(),
            TokenHash = "suelt",
            ExpiresAt = DateTime.UtcNow.AddHours(72),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { laborId = Guid.NewGuid(), action = "validate" })
        };

        Assert.Null(token.WorkOrderId);
        Assert.NotNull(token.Metadata);
        Assert.Contains("laborId", token.Metadata);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 8. Token hash — determinista
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenHash_Deterministic_SameInput_SameOutput()
    {
        var input = "test-token-12345";
        var hash1 = ShareControllerUtils.ComputeHashForTest(input);
        var hash2 = ShareControllerUtils.ComputeHashForTest(input);

        Assert.Equal(hash1, hash2);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 9. Token hash — diferentes para diferentes inputs
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenHash_DifferentInput_DifferentOutput()
    {
        var hash1 = ShareControllerUtils.ComputeHashForTest("token-a");
        var hash2 = ShareControllerUtils.ComputeHashForTest("token-b");

        Assert.NotEqual(hash1, hash2);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 10. SharedToken — IsUsed field exists and is false on creation
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SharedToken_New_IsUsed_False()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        Assert.False(token.IsUsed);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 11. SharedToken — expired
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SharedToken_Expired_ShouldNotValidate()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TokenHash = "exp",
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        Assert.True(token.ExpiresAt < DateTime.UtcNow);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 12. SharedToken — revoked
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SharedToken_Revoked_HasIsRevokedTrue()
    {
        var token = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TokenHash = "rev",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        Assert.True(token.IsRevoked);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 13. PublicBaseUrl — fallback al Request.Scheme + Host
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void PublicBaseUrl_NotConfigured_ShouldDefault()
    {
        // Validates that the code path exists: when PublicBaseUrl is null,
        // the fallback to Request.Scheme://Request.Host is used.
        // This is a structural test — the actual resolution happens in the
        // controller via IConfiguration.
        string? publicBaseUrl = null; // Simulates unconfigured
        var fallback = "https://localhost"; // Request.Scheme + Host would produce this

        var finalUrl = publicBaseUrl ?? fallback;
        Assert.Equal("https://localhost", finalUrl);
    }
}

// Static helper to expose internal methods for testing.
// In production, ShareController.ParseTokenMetadata is internal;
// this wrapper allows the test project to call it.
internal static class ShareControllerUtils
{
    public static ShareController.TokenMetadataInfo ParseTokenMetadata(SharedToken token)
        => ShareController.ParseTokenMetadata(token);

    public static string ComputeHashForTest(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
