using SQLite;

namespace RoseGuard.Models;

public class SecureNote
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[MaxLength(120)]
	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	// Date the note belongs to, stored in UTC midnight for consistency.
	[Indexed(Unique = true)]
	public DateTime NoteDateUtc { get; set; } = DateTime.UtcNow.Date;

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
