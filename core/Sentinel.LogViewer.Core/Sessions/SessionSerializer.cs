using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentinel.LogViewer.Core.Sessions;

/// <summary>
/// Writes and reads a session as compressed JSON
/// </summary>
public static class SessionSerializer
{
	/// <summary>The extension of a session file</summary>
	public const string Extension = ".slvsession";

	private static readonly JsonSerializerOptions Options = new()
	{
		Converters = { new JsonStringEnumConverter() },
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public static void Write(Stream stream, SessionData data)
	{
		using var zip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
		JsonSerializer.Serialize(zip, data, Options);
	}

	/// <exception cref="InvalidDataException">The stream is no session, or it was written by a newer version</exception>
	public static SessionData Read(Stream stream)
	{
		SessionData? data;
		try
		{
			using var zip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
			data = JsonSerializer.Deserialize<SessionData>(zip, Options);
		}
		catch (Exception ex) when (ex is JsonException or InvalidDataException)
		{
			throw new InvalidDataException("The file is not a session of Sentinel.LogViewer.", ex);
		}

		if (data == null)
			throw new InvalidDataException("The file is not a session of Sentinel.LogViewer.");

		if (data.Version > SessionData.CurrentVersion)
			throw new InvalidDataException($"The session was saved by a newer version of Sentinel.LogViewer (format {data.Version}).");

		return data;
	}

	/// <summary>
	/// Writes the session to a file; the old file stays whole until the new one is written
	/// </summary>
	public static void Save(string path, SessionData data)
	{
		var temporary = path + ".tmp";
		try
		{
			using (var stream = File.Create(temporary))
				Write(stream, data);

			File.Move(temporary, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporary))
				File.Delete(temporary);
		}
	}

	public static SessionData Load(string path)
	{
		using var stream = File.OpenRead(path);
		return Read(stream);
	}
}
