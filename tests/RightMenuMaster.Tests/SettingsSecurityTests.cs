using RightMenuMaster.Helpers;
using RightMenuMaster.Services;
using System.IO;
using Xunit;

namespace RightMenuMaster.Tests;

/// <summary>DPAPI 加密与 llm.json 的落盘格式。</summary>
public class SettingsSecurityTests : IDisposable
{
    private readonly string _settingsFile = AppPaths.LlmSettingsFile;
    private readonly string? _backup;

    public SettingsSecurityTests()
    {
        // 测试进程的数据目录是测试输出目录，但仍先备份以防万一
        if (File.Exists(_settingsFile)) _backup = File.ReadAllText(_settingsFile);
    }

    public void Dispose()
    {
        try
        {
            if (_backup != null) File.WriteAllText(_settingsFile, _backup);
            else if (File.Exists(_settingsFile)) File.Delete(_settingsFile);
        }
        catch { /* 忽略 */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Protect_Unprotect_往返一致()
    {
        const string secret = "sk-1234567890abcdefGHIJKL";

        var cipher = DataProtection.Protect(secret);

        Assert.NotNull(cipher);
        Assert.NotEqual(secret, cipher);
        Assert.DoesNotContain(secret, cipher);
        Assert.Equal(secret, DataProtection.Unprotect(cipher));
    }

    [Fact]
    public void Protect_中文与空值()
    {
        Assert.Equal(string.Empty, DataProtection.Protect(string.Empty));
        Assert.Equal(string.Empty, DataProtection.Unprotect(string.Empty));

        var cipher = DataProtection.Protect("密钥内容-中文测试");
        Assert.Equal("密钥内容-中文测试", DataProtection.Unprotect(cipher));
    }

    [Fact]
    public void Unprotect_损坏密文返回null()
    {
        Assert.Null(DataProtection.Unprotect("这不是合法的 base64!!!"));
        Assert.Null(DataProtection.Unprotect("aGVsbG8gd29ybGQ="));  // 合法 base64，但不是 DPAPI 密文
    }

    /// <summary>API Key 不能以明文出现在配置文件里。</summary>
    [Fact]
    public void LlmSettings_保存后文件中不含明文Key()
    {
        const string secret = "sk-MYSECRETKEY-9988776655";
        new LlmSettings
        {
            BaseUrl = "https://api.example.com/v1",
            Model = "gpt-test",
            Key = secret,
        }.Save();

        var raw = File.ReadAllText(_settingsFile);

        Assert.DoesNotContain(secret, raw);
        Assert.Contains("ProtectedApiKey", raw);
        // 明文字段保留但必须为空（仅为兼容旧文件而存在）
        Assert.Contains("\"ApiKey\": \"\"", raw);
    }

    [Fact]
    public void LlmSettings_保存后能原样读回()
    {
        const string secret = "sk-ROUNDTRIP-0001";
        new LlmSettings
        {
            BaseUrl = "https://api.example.com/v1",
            Model = "gpt-test",
            Key = secret,
        }.Save();

        var loaded = LlmSettings.Load();

        Assert.Equal("https://api.example.com/v1", loaded.BaseUrl);
        Assert.Equal("gpt-test", loaded.Model);
        Assert.Equal(secret, loaded.Key);
        Assert.True(loaded.IsConfigured);
    }

    /// <summary>旧版本写下的明文配置仍要能读出来，否则用户升级后 Key 就丢了。</summary>
    [Fact]
    public void LlmSettings_兼容旧的明文配置()
    {
        File.WriteAllText(_settingsFile, """
            {
              "BaseUrl": "https://old.example.com/v1",
              "ApiKey": "sk-OLD-PLAINTEXT-KEY",
              "Model": "old-model"
            }
            """);

        var loaded = LlmSettings.Load();

        Assert.Equal("sk-OLD-PLAINTEXT-KEY", loaded.Key);
        Assert.True(loaded.IsConfigured);

        // 再保存一次应自动升级为密文
        loaded.Save();
        Assert.DoesNotContain("sk-OLD-PLAINTEXT-KEY", File.ReadAllText(_settingsFile));
    }

    [Fact]
    public void LlmSettings_配置损坏时按未配置处理()
    {
        File.WriteAllText(_settingsFile, "{ 损坏的内容");

        var loaded = LlmSettings.Load();

        Assert.False(loaded.IsConfigured);
    }

    [Fact]
    public void AppPaths_数据目录必须可写()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppPaths.DataDir));
        Assert.True(Directory.Exists(AppPaths.DataDir));

        var probe = Path.Combine(AppPaths.DataDir, $"probe_{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "x");
        Assert.True(File.Exists(probe));
        File.Delete(probe);
    }
}
