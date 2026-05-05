using System.Security.Cryptography;
using System.Text.Json;

namespace SyncAgent_Core;

public static class ConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FarmaConecta");
    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.dat");

    public static void SaveConfig(AgentConfig config)
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        string json = JsonSerializer.Serialize(config);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

        // Encrypt the data using DPAPI
        byte[] encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);

        File.WriteAllBytes(ConfigFilePath, encryptedData);
    }

    public static AgentConfig? LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return null;
        }

        try
        {
            byte[] encryptedData = File.ReadAllBytes(ConfigFilePath);

            // Decrypt the data using DPAPI
            byte[] data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.LocalMachine);

            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<AgentConfig>(json);
        }
        catch
        {
            return null;
        }
    }
}
