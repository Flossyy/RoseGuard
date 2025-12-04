using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Maui.Storage;
using RoseGuard.Models;
using SQLite;

namespace RoseGuard.Services;

public sealed class DatabaseService
{
	private const string DatabaseFilename = "roseguard.db3";
	private const string KeyStorageKey = "roseguard_db_key";

	private SQLiteAsyncConnection? _connection;
	private Task? _initializationTask;

	public async Task InitializeAsync()
	{
		_initializationTask ??= InitInternalAsync();
		await _initializationTask.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<SecureNote>> GetNotesAsync()
	{
		var connection = await GetConnectionAsync().ConfigureAwait(false);
		return await connection.Table<SecureNote>()
			.OrderByDescending(n => n.NoteDateUtc)
			.ToListAsync()
			.ConfigureAwait(false);
	}

	public async Task<IReadOnlyList<SecureNote>> GetNotesForMonthAsync(DateTime monthLocal)
	{
		var connection = await GetConnectionAsync().ConfigureAwait(false);
		var start = new DateTime(monthLocal.Year, monthLocal.Month, 1, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
		var end = start.AddMonths(1);

		return await connection.Table<SecureNote>()
			.Where(n => n.NoteDateUtc >= start && n.NoteDateUtc < end)
			.ToListAsync()
			.ConfigureAwait(false);
	}

	public async Task<SecureNote?> GetNoteForDateAsync(DateOnly date)
	{
		var connection = await GetConnectionAsync().ConfigureAwait(false);
		var (start, end) = ToUtcRange(date);

		return await connection.Table<SecureNote>()
			.Where(n => n.NoteDateUtc >= start && n.NoteDateUtc < end)
			.FirstOrDefaultAsync()
			.ConfigureAwait(false);
	}

	public async Task SaveNoteForDateAsync(DateOnly date, string title, string body)
	{
		var connection = await GetConnectionAsync().ConfigureAwait(false);
		var (start, _) = ToUtcRange(date);

		var existing = await GetNoteForDateAsync(date).ConfigureAwait(false);
		if (existing is null)
		{
			var note = new SecureNote
			{
				Title = title,
				Body = body,
				NoteDateUtc = start,
				CreatedAtUtc = DateTime.UtcNow
			};
			await connection.InsertAsync(note).ConfigureAwait(false);
		}
		else
		{
			existing.Title = title;
			existing.Body = body;
			existing.NoteDateUtc = start;
			existing.CreatedAtUtc = DateTime.UtcNow;
			await connection.UpdateAsync(existing).ConfigureAwait(false);
		}
	}

	public async Task<int> DeleteAllAsync()
	{
		var connection = await GetConnectionAsync().ConfigureAwait(false);
		return await connection.DeleteAllAsync<SecureNote>().ConfigureAwait(false);
	}

	private async Task<SQLiteAsyncConnection> GetConnectionAsync()
	{
		await InitializeAsync().ConfigureAwait(false);
		return _connection ?? throw new InvalidOperationException("Database failed to initialize.");
	}

	private async Task InitInternalAsync()
	{
		// Wire up SQLCipher provider for encryption.
		SQLitePCL.Batteries_V2.Init();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
		var key = await GetOrCreateEncryptionKeyAsync().ConfigureAwait(false);

		var connectionString = new SQLiteConnectionString(
			dbPath,
			storeDateTimeAsTicks: true,
			key: key);

		_connection = new SQLiteAsyncConnection(connectionString);
		await _connection.CreateTableAsync<SecureNote>().ConfigureAwait(false);
	}

	private static (DateTime StartUtc, DateTime EndUtc) ToUtcRange(DateOnly date)
	{
		var startLocal = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
		var endLocal = startLocal.AddDays(1);
		return (startLocal.ToUniversalTime(), endLocal.ToUniversalTime());
	}

	private static async Task<string> GetOrCreateEncryptionKeyAsync()
	{
		var key = await SecureStorage.Default.GetAsync(KeyStorageKey).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(key))
		{
			key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

			try
			{
				await SecureStorage.Default.SetAsync(KeyStorageKey, key).ConfigureAwait(false);
			}
			catch
			{
				// SecureStorage can be unavailable on some platforms (e.g., unpackaged Windows).
				Preferences.Default.Set(KeyStorageKey, key);
			}
		}
		else
		{
			// Keep a non-secure backup to avoid losing the key if SecureStorage is cleared.
			if (string.IsNullOrWhiteSpace(Preferences.Default.Get<string>(KeyStorageKey, string.Empty)))
			{
				Preferences.Default.Set(KeyStorageKey, key);
			}
		}

		return key;
	}
}
