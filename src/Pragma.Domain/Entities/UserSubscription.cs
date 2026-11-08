using Pragma.Domain.Common;
using Pragma.Domain.Enums;

namespace Pragma.Domain.Entities;

public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public DateTime? ValidUntilUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public int DailyAiQuestionsUsed { get; set; }
    public DateTime? LastAiQuestionDateUtc { get; set; }
}
