# ShegerPay C# .NET SDK

Official C# .NET SDK for [ShegerPay](https://shegerpay.com) — Ethiopian payment verification.

## Installation

```bash
dotnet add package ShegerPay.SDK
```

Or via the NuGet Package Manager:

```
Install-Package ShegerPay.SDK
```

## Quick Start

```csharp
using ShegerPay;

var client = new ShegerPayClient("sk_live_...");

var response = await client.VerifyAsync("txn_abc123");

if (response.IsSuccess)
{
    Console.WriteLine($"Payment verified: {response.TransactionId}");
    Console.WriteLine($"Amount: {response.Amount}");
}
else
{
    Console.WriteLine($"Verification failed: {response.Message}");
}
```

## API Reference

### `new ShegerPayClient(apiKey)`

Creates a new ShegerPay client.

| Parameter | Type   | Description        |
|-----------|--------|--------------------|
| `apiKey`  | string | Your secret API key |

### `client.VerifyAsync(transactionId)`

Asynchronously verifies a payment transaction.

| Parameter       | Type   | Description              |
|-----------------|--------|--------------------------|
| `transactionId` | string | The transaction ID to verify |

Returns a `VerifyResponse` with:
- `IsSuccess` — whether the payment was successful
- `TransactionId` — the transaction ID
- `Amount` — the verified amount
- `Message` — status message

## Requirements

- .NET 6.0+ (or .NET Standard 2.0+)

## License

MIT — see [LICENSE](LICENSE)
