using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SyncAgent_Core;

namespace SyncAgent_Service;

public class SyncApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SyncApiClient> _logger;

    public SyncApiClient(HttpClient httpClient, ILogger<SyncApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendDeltaAsync(AgentConfig config, SyncPayload payload)
    {
        try
        {
            string jsonPayload = JsonSerializer.Serialize(payload);
            string encryptedPayloadBase64 = EncryptPayload(jsonPayload, config.IntegrationKey);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.farmaconecta.com.br/api/v1/integracao/sync/delta/");
            request.Headers.Add("X-Tenant-ID", config.TenantId);
            request.Content = new StringContent(encryptedPayloadBase64, Encoding.UTF8, "text/plain");

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to send delta. Status Code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending delta payload to API.");
            return false;
        }
    }

    private string EncryptPayload(string plainText, string keyString)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(keyString);
        if (keyBytes.Length != 32)
        {
            throw new ArgumentException("IntegrationKey must be exactly 32 bytes for AES-256.");
        }

        using Aes aesAlg = Aes.Create();
        aesAlg.Key = keyBytes;
        aesAlg.GenerateIV();
        byte[] ivBytes = aesAlg.IV;

        // Create an encryptor to perform the stream transform.
        ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

        byte[] encryptedBytes;
        using (MemoryStream msEncrypt = new MemoryStream())
        {
            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    //Write all data to the stream.
                    swEncrypt.Write(plainText);
                }
                encryptedBytes = msEncrypt.ToArray();
            }
        }

        byte[] finalBytes = new byte[ivBytes.Length + encryptedBytes.Length];
        Buffer.BlockCopy(ivBytes, 0, finalBytes, 0, ivBytes.Length);
        Buffer.BlockCopy(encryptedBytes, 0, finalBytes, ivBytes.Length, encryptedBytes.Length);

        return Convert.ToBase64String(finalBytes);
    }
}
