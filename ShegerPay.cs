using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShegerPay.SDK
{
    /// <summary>
    /// ShegerPay C# SDK
    /// Official C# SDK for ShegerPay Payment Verification Gateway
    /// 
    /// Usage:
    ///   var client = new ShegerPayClient("sk_test_xxx");
    ///   var result = await client.VerifyAsync("FT123456", 100, provider: "cbe");
    /// </summary>
    public class ShegerPayClient : IDisposable
    {
        private const string Version = "1.0.0";
        private const string DefaultBaseUrl = "https://api.shegerpay.com";
        
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _mode;
        private readonly HttpClient _httpClient;
        
        /// <summary>
        /// Create a new ShegerPay client
        /// </summary>
        /// <param name="apiKey">Your secret API key (sk_test_xxx or sk_live_xxx)</param>
        public ShegerPayClient(string apiKey) : this(apiKey, DefaultBaseUrl) { }
        
        /// <summary>
        /// Create a new ShegerPay client with custom base URL
        /// </summary>
        public ShegerPayClient(string apiKey, string baseUrl)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key is required");
            
            if (!apiKey.StartsWith("sk_test_") && !apiKey.StartsWith("sk_live_"))
                throw new ArgumentException("Invalid API key format");
            
            _apiKey = apiKey;
            _baseUrl = baseUrl.TrimEnd('/');
            _mode = apiKey.StartsWith("sk_test_") ? "test" : "live";
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"ShegerPay-CSharp-SDK/{Version}");
        }
        
        /// <summary>
        /// Verify a payment transaction
        /// </summary>
        public async Task<VerificationResult> VerifyAsync(string transactionId, double amount, 
            string provider = null, string merchantName = null, string senderAccount = null)
        {
            if (string.IsNullOrEmpty(transactionId))
                throw new ShegerPayException("Transaction ID is required");
            
            provider ??= transactionId.ToLower().Contains("cs.bankofabyssinia.com/slip/?trx=") ? "boa" : null;
            if (string.IsNullOrEmpty(provider))
                throw new ShegerPayException("provider is required for ambiguous transaction references. Pass provider explicitly or use QuickVerifyAsync().");
            merchantName ??= "ShegerPay Verification";
            
            var data = new Dictionary<string, string>
            {
                ["provider"] = provider,
                ["transaction_id"] = transactionId,
                ["amount"] = amount.ToString(),
                ["merchant_name"] = merchantName
            };
            if (!string.IsNullOrEmpty(senderAccount))
                data["sender_account"] = senderAccount;
            
            var response = await DoRequestAsync("POST", "/api/v1/verify", data);
            return new VerificationResult(response);
        }
        
        /// <summary>
        /// Quick verification with auto-detected provider
        /// </summary>
        public async Task<VerificationResult> QuickVerifyAsync(string transactionId, double amount, string expectedProvider = null, string senderAccount = null)
        {
            var data = new Dictionary<string, string>
            {
                ["transaction_id"] = transactionId,
                ["amount"] = amount.ToString()
            };
            if (!string.IsNullOrEmpty(expectedProvider))
                data["expected_provider"] = expectedProvider;
            if (!string.IsNullOrEmpty(senderAccount))
                data["sender_account"] = senderAccount;
            
            var response = await DoRequestAsync("POST", "/api/v1/quick-verify", data);
            return new VerificationResult(response);
        }
        
        /// <summary>
        /// Get transaction history
        /// </summary>
        public async Task<List<Dictionary<string, object>>> GetHistoryAsync()
        {
            var response = await DoRequestAsync("GET", "/api/v1/history", null);
            // Parse as list
            return new List<Dictionary<string, object>>();
        }
        
        private async Task<Dictionary<string, object>> DoRequestAsync(string method, string path, 
            Dictionary<string, string> data)
        {
            var url = $"{_baseUrl}{path}";
            
            HttpResponseMessage response;
            
            if (method == "POST" && data != null)
            {
                var content = new FormUrlEncodedContent(data);
                response = await _httpClient.PostAsync(url, content);
            }
            else
            {
                response = await _httpClient.GetAsync(url);
            }
            
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new ShegerPayException("Invalid API key");
            
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                throw new ShegerPayException($"Validation error: {responseBody}");
            
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private async Task<Dictionary<string, object>> DoJsonRequestAsync(string method, string path,
            Dictionary<string, object> data = null)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), $"{_baseUrl}{path}");
            if (data != null && method != "GET" && method != "DELETE")
            {
                request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return new Dictionary<string, object>();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new ShegerPayException("Invalid API key");
            if (!response.IsSuccessStatusCode)
                throw new ShegerPayException($"ShegerPay error: {responseBody}");

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        public Task<Dictionary<string, object>> CreatePromoCodeAsync(Dictionary<string, object> parameters) =>
            DoJsonRequestAsync("POST", "/api/v1/promo-codes/", PromoPayload(parameters));

        public async Task<List<Dictionary<string, object>>> ListPromoCodesAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/promo-codes/");
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new ShegerPayException("Invalid API key");
            if (!response.IsSuccessStatusCode)
                throw new ShegerPayException($"ShegerPay error: {responseBody}");
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(responseBody) ?? new List<Dictionary<string, object>>();
        }

        public Task<Dictionary<string, object>> UpdatePromoCodeAsync(string codeId, Dictionary<string, object> parameters) =>
            DoJsonRequestAsync("PATCH", $"/api/v1/promo-codes/{codeId}", PromoPayload(parameters));

        public Task<Dictionary<string, object>> DeletePromoCodeAsync(string codeId) =>
            DoJsonRequestAsync("DELETE", $"/api/v1/promo-codes/{codeId}");

        public Task<Dictionary<string, object>> ValidatePromoCodeAsync(string code, double amount, Dictionary<string, object> options = null)
        {
            var payload = new Dictionary<string, object> { ["code"] = code, ["amount"] = amount };
            if (options != null)
                foreach (var item in options)
                    payload[item.Key] = item.Value;
            return DoJsonRequestAsync("POST", "/api/v1/promo-codes/validate", payload);
        }

        public Task<Dictionary<string, object>> RedeemPromoCodeAsync(string code, double amount, string transactionId, Dictionary<string, object> options = null)
        {
            var payload = new Dictionary<string, object> {
                ["code"] = code,
                ["amount"] = amount,
                ["transaction_id"] = transactionId
            };
            if (options != null)
                foreach (var item in options)
                    payload[item.Key] = item.Value;
            return DoJsonRequestAsync("POST", "/api/v1/promo-codes/redeem", payload);
        }

        public Task<Dictionary<string, object>> ApplyPaymentLinkCouponAsync(string shortCode, string code, double? amount = null, int quantity = 1, string provider = null, string customerIdentifier = null)
        {
            var payload = new Dictionary<string, object> { ["code"] = code, ["quantity"] = quantity };
            if (amount.HasValue)
                payload["amount"] = amount.Value;
            if (!string.IsNullOrWhiteSpace(provider))
                payload["provider"] = provider;
            if (!string.IsNullOrWhiteSpace(customerIdentifier))
                payload["customer_identifier"] = customerIdentifier;
            return DoJsonRequestAsync("POST", $"/api/v1/payment-links/{shortCode}/apply-coupon", payload);
        }

        public Task<Dictionary<string, object>> GetPaymentLinkOrderStatusAsync(string shortCode, string orderId)
        {
            return DoRequestAsync("GET", $"/api/v1/payment-links/{shortCode}/orders/{orderId}/status", null);
        }

        private Dictionary<string, object> PromoPayload(Dictionary<string, object> parameters)
        {
            var payload = new Dictionary<string, object>();
            if (parameters == null) return payload;
            foreach (var item in parameters)
                payload[ToSnakeCase(item.Key)] = item.Value;
            return payload;
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var builder = new StringBuilder();
            foreach (var ch in value)
            {
                if (char.IsUpper(ch))
                    builder.Append('_').Append(char.ToLowerInvariant(ch));
                else
                    builder.Append(ch);
            }
            return builder.ToString();
        }
        
        /// <summary>
        /// Verify webhook signature
        /// </summary>
        public static bool VerifyWebhookSignature(string payload, string signature, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();
            return expected == signature;
        }

        public static bool VerifyRedirectSignature(Dictionary<string, object> parameters, string signature, string secret)
        {
            var amount = Convert.ToDouble(GetRedirectParam(parameters, "amount", "amount") ?? 0);
            var payload = string.Join("|", new[]
            {
                GetRedirectString(parameters, "checkout_session_id", "checkoutSessionId", ""),
                GetRedirectString(parameters, "order_id", "orderId", ""),
                GetRedirectString(parameters, "short_code", "shortCode", ""),
                amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                GetRedirectString(parameters, "currency", "currency", "ETB"),
                GetRedirectString(parameters, "status", "status", "paid")
            });
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = BitConverter.ToString(hash).Replace("-", "").ToLower();
            return expected == (signature ?? "").Replace("sha256=", "");
        }

        private static object GetRedirectParam(Dictionary<string, object> parameters, string snake, string camel)
        {
            if (parameters == null) return null;
            if (parameters.TryGetValue(snake, out var snakeValue) && snakeValue != null) return snakeValue;
            if (parameters.TryGetValue(camel, out var camelValue) && camelValue != null) return camelValue;
            return null;
        }

        private static string GetRedirectString(Dictionary<string, object> parameters, string snake, string camel, string fallback)
        {
            var value = Convert.ToString(GetRedirectParam(parameters, snake, camel));
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
    
    /// <summary>
    /// Verification result
    /// </summary>
    public class VerificationResult
    {
        public bool Valid { get; }
        public string Status { get; }
        public string Provider { get; }
        public string TransactionId { get; }
        public double? Amount { get; }
        public string Reason { get; }
        public string Mode { get; }
        
        public VerificationResult(Dictionary<string, object> data)
        {
            if (data.TryGetValue("valid", out var valid))
                Valid = valid is JsonElement el && el.GetBoolean();
            
            if (data.TryGetValue("status", out var status))
                Status = status?.ToString();
            
            if (data.TryGetValue("provider", out var provider))
                Provider = provider?.ToString();
            
            if (data.TryGetValue("transaction_id", out var txId))
                TransactionId = txId?.ToString();
            
            if (data.TryGetValue("amount", out var amount) && amount is JsonElement amountEl)
                Amount = amountEl.GetDouble();
            
            if (data.TryGetValue("reason", out var reason))
                Reason = reason?.ToString();
            
            if (data.TryGetValue("mode", out var mode))
                Mode = mode?.ToString();
        }
        
        public bool IsValid() => Valid;
    }
    
    /// <summary>
    /// ShegerPay SDK Exception
    /// </summary>
    public class ShegerPayException : Exception
    {
        public ShegerPayException(string message) : base(message) { }
    }
}
