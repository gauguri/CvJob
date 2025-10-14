using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace F500.JobMatch.Api.Data;

public class Resume
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}

public class JobPosting
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string StableIdHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Company { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Location { get; set; }

    public string? DescriptionHtml { get; set; }

    public string DescriptionText { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? EmploymentType { get; set; }

    public DateTime? PostedAtUtc { get; set; }

    [MaxLength(512)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Source { get; set; }

    public DateTime FetchedAtUtc { get; set; }
}
