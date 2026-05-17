param(
    [string]$SettingsPath = "$env:LOCALAPPDATA\VitaMR\settings.json",
    [string]$SecretsPath = "$env:LOCALAPPDATA\VitaMR\Secrets\Gemini_Secrets.v1.json"
)

$ErrorActionPreference = 'Stop'

$dpapiCode = @'
using System;
using System.Runtime.InteropServices;

public static class VitaMrGeminiSecretReader
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        string szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Unprotect(byte[] data, byte[] entropy)
    {
        DATA_BLOB input = ToBlob(data);
        DATA_BLOB ent = ToBlob(entropy);
        DATA_BLOB output = new DATA_BLOB();

        try
        {
            if (!CryptUnprotectData(ref input, null, ref ent, IntPtr.Zero, IntPtr.Zero, 0, ref output))
            {
                throw new InvalidOperationException("CryptUnprotectData failed.");
            }

            byte[] result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            if (input.pbData != IntPtr.Zero) Marshal.FreeHGlobal(input.pbData);
            if (ent.pbData != IntPtr.Zero) Marshal.FreeHGlobal(ent.pbData);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    private static DATA_BLOB ToBlob(byte[] bytes)
    {
        var blob = new DATA_BLOB
        {
            cbData = bytes.Length,
            pbData = Marshal.AllocHGlobal(bytes.Length)
        };

        Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
        return blob;
    }
}
'@

Add-Type -TypeDefinition $dpapiCode

if (-not (Test-Path $SettingsPath)) {
    throw "Settings file not found: $SettingsPath"
}

if (-not (Test-Path $SecretsPath)) {
    throw "Gemini secret file not found: $SecretsPath"
}

$settings = Get-Content -Path $SettingsPath -Raw | ConvertFrom-Json
$record = Get-Content -Path $SecretsPath -Raw | ConvertFrom-Json
$entropy = [Text.Encoding]::UTF8.GetBytes('VitaMR.GeminiSecrets.v1')

$fastKey = [Text.Encoding]::UTF8.GetString(
    [VitaMrGeminiSecretReader]::Unprotect(
        [Convert]::FromBase64String($record.fastApiKeyProtected),
        $entropy))
$thinkingKey = [Text.Encoding]::UTF8.GetString(
    [VitaMrGeminiSecretReader]::Unprotect(
        [Convert]::FromBase64String($record.thinkingApiKeyProtected),
        $entropy))

if ([string]::IsNullOrWhiteSpace($fastKey) -or [string]::IsNullOrWhiteSpace($thinkingKey)) {
    throw "Gemini keys decrypted as empty."
}

function Test-GeminiModel {
    param(
        [string]$Label,
        [string]$ModelName,
        [string]$ApiKey
    )

    $uri = "https://generativelanguage.googleapis.com/v1beta/models/$([uri]::EscapeDataString($ModelName)):generateContent?key=$([uri]::EscapeDataString($ApiKey))"
    $body = @{
        contents = @(
            @{
                role = 'user'
                parts = @(
                    @{
                        text = "Return JSON only: {`"status`":`"ok`",`"label`":`"$Label`"}"
                    }
                )
            }
        )
        generationConfig = @{
            temperature = 0
            responseMimeType = 'application/json'
        }
    } | ConvertTo-Json -Depth 8

    try {
        $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 45
        $text = $response.candidates[0].content.parts[0].text
        [pscustomobject]@{
            Label = $Label
            Model = $ModelName
            Status = 'OK'
            Detail = "Response text length $($text.Length)"
        }
    }
    catch {
        $statusCode = ''

        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        [pscustomobject]@{
            Label = $Label
            Model = $ModelName
            Status = 'FAILED'
            Detail = "HTTP/status $statusCode - $($_.Exception.Message)"
        }
    }
}

Write-Output "Gemini key decrypt check: OK"
Test-GeminiModel -Label 'fast' -ModelName $settings.GeminiFastModelName -ApiKey $fastKey
Test-GeminiModel -Label 'thinking' -ModelName $settings.GeminiThinkingModelName -ApiKey $thinkingKey
