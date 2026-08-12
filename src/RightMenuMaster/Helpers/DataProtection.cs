using System.Runtime.InteropServices;
using System.Text;

namespace RightMenuMaster.Helpers;

/// <summary>
/// 用 Windows DPAPI 按「当前用户」加密小段敏感文本（此处用于 AI 接口的 API Key）。
/// 密文换台机器或换个用户都解不开，避免配置文件被直接读走。
///
/// 直接 P/Invoke CryptProtectData，不引入 System.Security.Cryptography.ProtectedData
/// 包，保持项目零 NuGet 依赖（便于单文件发布）。
/// </summary>
public static class DataProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    private const uint CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, uint dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>加密为 base64 字符串；失败时返回 null（调用方自行决定是否降级明文）。</summary>
    public static string? Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            input.pbData = Marshal.AllocHGlobal(bytes.Length);
            input.cbData = bytes.Length;
            Marshal.Copy(bytes, 0, input.pbData, bytes.Length);

            if (!CryptProtectData(ref input, "RightMenuMaster", IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output))
                return null;

            var encrypted = new byte[output.cbData];
            Marshal.Copy(output.pbData, encrypted, 0, output.cbData);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    /// <summary>解密 base64 密文；失败（换机器/换用户/内容损坏）时返回 null。</summary>
    public static string? Unprotect(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;

        var input = new DATA_BLOB();
        var output = new DATA_BLOB();
        try
        {
            var bytes = Convert.FromBase64String(base64);
            input.pbData = Marshal.AllocHGlobal(bytes.Length);
            input.cbData = bytes.Length;
            Marshal.Copy(bytes, 0, input.pbData, bytes.Length);

            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, out output))
                return null;

            var decrypted = new byte[output.cbData];
            Marshal.Copy(output.pbData, decrypted, 0, output.cbData);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }
}
