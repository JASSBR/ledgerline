using System.Security.Cryptography;
using System.Text;
using Ledgerline.SharedKernel;

namespace Ledgerline.Payments.Domain;

public sealed record TransferRequest(Guid FromAccountId, string ToIban, decimal Amount, string? Label);

public static class PaymentErrors
{
    public static readonly Error AmountInvalid = Error.Validation("payments.amount_invalid", "The amount must be positive, with at most two decimals, and below 1,000,000 €.");

    public static readonly Error LabelTooLong = Error.Validation("payments.label_too_long", "The label is limited to 140 characters (SEPA remittance information).");

    public static readonly Error IdempotencyKeyMissing = Error.Validation("payments.idempotency_key_missing", "An Idempotency-Key header (1 to 64 characters) is required.");

    public static readonly Error IdempotencyKeyReused = Error.Conflict("payments.idempotency_key_reused", "This Idempotency-Key was already used for a different transfer.");

    public static readonly Error SourceAccountNotFound = Error.NotFound("payments.source_account_not_found", "No account of yours has this id.");

    public static readonly Error BeneficiaryUnknown = Error.Validation("payments.beneficiary_unknown", "No account at this bank has this IBAN.");

    public static readonly Error SameAccount = Error.Validation("payments.same_account", "The beneficiary is the paying account.");

    public static readonly Error TransferNotFound = Error.NotFound("payments.transfer_not_found", "No transfer of yours has this id.");
}

public static class TransferRequestRules
{
    public const int LabelMaxLength = 140;

    public static Result Validate(TransferRequest request, string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
        {
            return PaymentErrors.IdempotencyKeyMissing;
        }

        if (request.Amount <= 0 || request.Amount >= 1_000_000 || decimal.Round(request.Amount, 2) != request.Amount)
        {
            return PaymentErrors.AmountInvalid;
        }

        return request.Label is { Length: > LabelMaxLength } ? PaymentErrors.LabelTooLong : Result.Success();
    }

    /// <summary>
    /// Identifies the *content* of a request. Same key + same fingerprint = a retry (answer with the original transfer);
    /// same key + different fingerprint = a client bug (refuse, never guess).
    /// </summary>
    public static string Fingerprint(TransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var canonical = string.Join('|', request.FromAccountId.ToString("N"), request.ToIban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
            request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), request.Label?.Trim() ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
