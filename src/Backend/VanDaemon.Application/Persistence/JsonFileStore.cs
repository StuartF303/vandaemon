using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VanDaemon.Application.Persistence;

/// <summary>
/// Simple JSON file-based persistence for application data
/// </summary>
public class JsonFileStore
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonFileStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonFileStore(ILogger<JsonFileStore> logger, string? dataDirectory = null)
    {
        _logger = logger;
        _dataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");

        // Ensure data directory exists
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Created data directory at {Path}", _dataDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Save data to a JSON file
    /// </summary>
    public async Task SaveAsync<T>(string fileName, T data)
    {
        var filePath = Path.Combine(_dataDirectory, fileName);

        await _semaphore.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Saved data to {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving data to {FileName}", fileName);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Load data from a JSON file
    /// </summary>
    public async Task<T?> LoadAsync<T>(string fileName) where T : class
    {
        var filePath = Path.Combine(_dataDirectory, fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("File {FileName} does not exist, returning null", fileName);
            return null;
        }

        await _semaphore.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            _logger.LogDebug("Loaded data from {FileName}", fileName);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data from {FileName}", fileName);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Delete a JSON file
    /// </summary>
    public async Task DeleteAsync(string fileName)
    {
        var filePath = Path.Combine(_dataDirectory, fileName);

        await _semaphore.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Deleted file {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileName}", fileName);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Check if a file exists
    /// </summary>
    public bool Exists(string fileName)
    {
        var filePath = Path.Combine(_dataDirectory, fileName);
        return File.Exists(filePath);
    }

    /// <summary>
    /// Get all files in the data directory
    /// </summary>
    public IEnumerable<string> GetAllFiles()
    {
        return Directory.GetFiles(_dataDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!);
    }
}
