using System;
using Pragma.Domain.Common;

namespace Pragma.Domain.Entities;

/// <summary>
/// Kohort üzrə tələbənin Final layihə linki təhvili.
/// </summary>
public class FinalSubmission : BaseEntity
{
    public Guid CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>
    /// Tələbənin təhvil verdiyi layihə URL-i (GitHub repo, canlı demo və s. - max 500 simvol).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// İlk dəfə təhvil verilmə tarixi və saatı (UTC).
    /// </summary>
    public DateTime SubmittedAtUtc { get; set; }
}
