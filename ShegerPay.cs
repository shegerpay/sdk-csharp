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
