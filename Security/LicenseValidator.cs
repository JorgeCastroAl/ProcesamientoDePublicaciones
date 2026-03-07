using System;
using System.IO;
using System.Reflection;
using Serilog;
using TikTokSuite.Licensing;

namespace FluxAnswer.Security;

internal static class LicenseValidator
{
    public static LicenseValidationResult ValidateForCurrentMachine(string baseDirectory, bool isDevelopmentMode)
    {
        Log.Information("[LICENSE] Starting validation. DevMode={IsDevelopmentMode}, BaseDirectory={BaseDirectory}", isDevelopmentMode, baseDirectory);

        if (isDevelopmentMode)
        {
            Log.Information("[LICENSE] Validation skipped because application is running in development mode.");
            return LicenseValidationResult.Valid("License check skipped in development mode.");
        }

        var localAppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TikTokManager");

        var machineHash = MachineFingerprint.ComputeCurrentMachineHash();
        var protectedSerialPath = Path.Combine(localAppDataDir, "license.serial.protected.bin");
        var serialPath = Path.Combine(localAppDataDir, "license.serial");
        Log.Debug("[LICENSE] Serial candidates: protected={ProtectedSerialPath}, plain={SerialPath}", protectedSerialPath, serialPath);

        var serial = ProtectedLicenseStore.TryLoadProtectedText(protectedSerialPath);
        if (!string.IsNullOrWhiteSpace(serial))
        {
            Log.Information("[LICENSE] Loaded protected serial from LocalAppData store.");
        }

        if (string.IsNullOrWhiteSpace(serial) && File.Exists(serialPath))
        {
            serial = File.ReadAllText(serialPath);
            Log.Information("[LICENSE] Loaded plain serial file from: {SerialPath}", serialPath);
        }

        if (!string.IsNullOrWhiteSpace(serial))
        {
            Log.Debug("[LICENSE] Validating serial against current machine fingerprint.");
            return SerialLicenseService.ValidateSerial(machineHash, serial)
                ? LicenseValidationResult.Valid("Serial license validated.")
                : LicenseValidationResult.Invalid("Serial is invalid for this machine.");
        }

        // Legacy fallback for previously-issued JSON licenses.
        var licensePath = Path.Combine(localAppDataDir, "license.json");
        var protectedLicensePath = Path.Combine(localAppDataDir, "license.protected.bin");
        Log.Debug("[LICENSE] No serial found. Trying legacy JSON license: protected={ProtectedLicensePath}, plain={LicensePath}", protectedLicensePath, licensePath);

        var licenseJson = ProtectedLicenseStore.TryLoadProtectedText(protectedLicensePath);
        if (string.IsNullOrWhiteSpace(licenseJson) && File.Exists(licensePath))
        {
            licenseJson = File.ReadAllText(licensePath);
            Log.Information("[LICENSE] Loaded legacy plain license file from: {LicensePath}", licensePath);
        }

        if (string.IsNullOrWhiteSpace(licenseJson))
        {
            Log.Error("[LICENSE] No serial or legacy license file was found in LocalAppData.");
            return LicenseValidationResult.Invalid($"Serial file was not found: {serialPath}");
        }

        var publicKeyPath = ResolvePublicKeyPath(baseDirectory, localAppDataDir);
        if (publicKeyPath is null)
        {
            Log.Error("[LICENSE] Public key file was not found in env/local/app locations.");
            return LicenseValidationResult.Invalid("Public key file was not found. Expected 'license_public.pem' in LocalAppData or app directory, or env TIKTOKSUITE_LICENSE_PUBLIC_KEY_PATH.");
        }
        Log.Information("[LICENSE] Using public key from: {PublicKeyPath}", publicKeyPath);

        var publicKeyPem = ProtectedLicenseStore.TryLoadProtectedText(Path.Combine(localAppDataDir, "license_public.protected.bin"));
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            publicKeyPem = File.ReadAllText(publicKeyPath);
            Log.Debug("[LICENSE] Loaded public key as plain text from resolved path.");
        }
        else
        {
            Log.Debug("[LICENSE] Loaded protected public key from LocalAppData store.");
        }

        try
        {
            Log.Debug("[LICENSE] Validating legacy signed license payload.");
            return LicenseVerifier.Validate(
                licenseJson,
                publicKeyPem,
                machineHash,
                GetCurrentAppMajorVersion());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "License validation failed due to exception");
            return LicenseValidationResult.Invalid($"License validation failed: {ex.Message}");
        }
    }

    private static string? ResolvePublicKeyPath(string baseDirectory, string localAppDataDir)
    {
        var fromEnv = Environment.GetEnvironmentVariable("TIKTOKSUITE_LICENSE_PUBLIC_KEY_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
        {
            Log.Debug("[LICENSE] Public key resolved from environment variable.");
            return fromEnv;
        }

        var localPath = Path.Combine(localAppDataDir, "license_public.pem");
        if (File.Exists(localPath))
        {
            Log.Debug("[LICENSE] Public key resolved from LocalAppData.");
            return localPath;
        }

        var appPath = Path.Combine(baseDirectory, "license_public.pem");
        if (File.Exists(appPath))
        {
            Log.Debug("[LICENSE] Public key resolved from app directory.");
            return appPath;
        }

        return null;
    }

    private static int GetCurrentAppMajorVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version?.Major ?? 1;
    }
}
