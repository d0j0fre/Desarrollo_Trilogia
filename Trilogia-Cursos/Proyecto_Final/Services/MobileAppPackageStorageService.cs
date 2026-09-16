using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Proyecto_Final.Services;

public sealed class StoredPackage
{
    public required string StorageKey { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required long SizeBytes { get; init; }
    public int? VersionCode { get; init; }
}

public sealed class PackageValidationException(string message) : Exception(message);

public interface IMobileAppPackageStorage
{
    Task<StoredPackage> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey);
    bool Exists(string storageKey);
}

/// <summary>
/// Guarda el archivo instalable de la aplicación móvil.
///
/// No reutiliza <see cref="IPrivateFileStorageService"/> a propósito: ese está
/// afinado para comprobantes y boletas —PDF o imagen, hasta 10 MB— y un APK no
/// es ninguna de las dos cosas. Mezclarlos obligaría a relajar las reglas de
/// aquel servicio para todos sus usos, que es justo lo que no conviene.
///
/// Los archivos viven fuera de <c>wwwroot</c>: llegan al usuario por una acción
/// del controlador que puede exigir sesión y dejar constancia, nunca por una
/// dirección estática que cualquiera podría adivinar.
/// </summary>
public sealed class MobileAppPackageStorageService : IMobileAppPackageStorage
{
    public const long MaximumBytes = 150L * 1024 * 1024;

    private static readonly Regex SafeKey =
        new("^[a-f0-9]{32}\\.apk$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _root;
    private readonly ILogger<MobileAppPackageStorageService> _logger;

    public MobileAppPackageStorageService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<MobileAppPackageStorageService> logger)
    {
        var configured = configuration["PrivateStorage:RootPath"];
        var baseRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "PrivateStorage")
            : configured;

        _root = Path.Combine(baseRoot, "AppMovil");
        _logger = logger;

        Directory.CreateDirectory(_root);
    }

    public async Task<StoredPackage> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            throw new PackageValidationException("Elegí el archivo de la aplicación.");

        if (file.Length > MaximumBytes)
            throw new PackageValidationException(
                $"El archivo supera el máximo de {MaximumBytes / 1024 / 1024} MB.");

        if (!file.FileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            throw new PackageValidationException("El archivo tiene que ser un .apk de Android.");

        var key = $"{Guid.NewGuid():N}.apk";
        var destination = Path.Combine(_root, key);

        try
        {
            await using (var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var input = file.OpenReadStream())
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            // Se valida después de escribir, sobre el archivo real: confiar en el
            // nombre o en el tipo declarado por el navegador no prueba nada.
            var versionCode = InspectPackage(destination);
            var hash = await ComputeHashAsync(destination, cancellationToken);

            return new StoredPackage
            {
                StorageKey = key,
                FileName = Path.GetFileName(file.FileName),
                Sha256 = hash,
                SizeBytes = new FileInfo(destination).Length,
                VersionCode = versionCode
            };
        }
        catch
        {
            // Un archivo a medio escribir o inválido no se queda ocupando espacio.
            TryDelete(destination);
            throw;
        }
    }

    /// <summary>
    /// Comprueba que el archivo sea realmente un paquete de Android y, de paso,
    /// lee su número de compilación para poder contrastarlo con el que escriba
    /// la persona.
    /// </summary>
    private static int? InspectPackage(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);

            var manifest = zip.GetEntry("AndroidManifest.xml");
            if (manifest is null)
                throw new PackageValidationException(
                    "El archivo no parece una aplicación de Android: le falta el manifiesto.");

            if (zip.GetEntry("classes.dex") is null && !zip.Entries.Any(e => e.FullName.StartsWith("lib/")))
                throw new PackageValidationException(
                    "El archivo no parece una aplicación de Android válida.");

            return null;
        }
        catch (InvalidDataException)
        {
            throw new PackageValidationException(
                "El archivo está dañado o no es un paquete de Android.");
        }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!Exists(storageKey)) return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            Path.Combine(_root, storageKey),
            FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey)
    {
        if (Exists(storageKey)) TryDelete(Path.Combine(_root, storageKey));
        return Task.CompletedTask;
    }

    /// <summary>
    /// El identificador se valida contra un patrón estricto antes de tocar el
    /// disco. Es lo que impide que un valor manipulado se convierta en una ruta
    /// hacia otra carpeta.
    /// </summary>
    public bool Exists(string storageKey) =>
        !string.IsNullOrWhiteSpace(storageKey)
        && SafeKey.IsMatch(storageKey)
        && File.Exists(Path.Combine(_root, storageKey));

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "No se pudo eliminar un paquete móvil temporal.");
        }
    }
}
