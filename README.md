<p align="center"><img src="logo.png" alt="ShegerPay" width="200" /></p>

# ShegerPay C# / .NET SDK

[![Version](https://img.shields.io/badge/version-2.2.0-blue)](https://www.nuget.org/packages/ShegerPay.SDK)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Official C# SDK for ShegerPay — verify Ethiopian bank payments (CBE, Telebirr, BOA, Awash).

## Install

```bash
dotnet add package ShegerPay.SDK
```

## Quick Start

```csharp
using ShegerPay;

var client = new ShegerPayClient("sk_live_YOUR_API_KEY");

// Verify a payment
var result = await client.VerifyAsync("FT26062K7WMY", provider: "cbe", amount: 1000);
Console.WriteLine(result.Verified); // true/false

// Verify without amount (lookup only)
var result2 = await client.VerifyAsync("FT26062K7WMY", provider: "telebirr");
Console.WriteLine(result2.Status);

// Verify from receipt screenshot
var imageBytes = File.ReadAllBytes("receipt.png");
var imageBase64 = Convert.ToBase64String(imageBytes);
var imgResult = await client.VerifyImageAsync(imageBase64, provider: "cbe");
Console.WriteLine(imgResult.Verified);

// Create payment link
var link = await client.CreatePaymentLinkAsync(new {
    title = "Order #1234",
    amount = 1500,
    currency = "ETB"
});
Console.WriteLine(link.Url);

// Webhook signature check
bool valid = ShegerPayClient.VerifyWebhookSignature(payload, signature, secret);
```

## Supported Providers
`cbe` · `telebirr` · `boa` · `awash` · `ebirr_kaafi` · `ebirr_coop`

## Requirements
- .NET 6.0+


## Support
- 📚 Docs: https://shegerpay.com/docs
- 💬 Telegram: [@shegerpay_0](https://t.me/shegerpay_0)
- 📧 Email: support@shegerpay.com
