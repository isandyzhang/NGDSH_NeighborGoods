using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Messaging.Services;

public sealed class QuickResponderEvaluationService(
    NeighborGoodsDbContext dbContext,
    ILogger<QuickResponderEvaluationService> logger)
{
    private static readonly TimeSpan EvaluationWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxReasonableReplyDelay = TimeSpan.FromDays(3);
    private const int QuickResponderThresholdMinutes = 120;

    public async Task<QuickResponderEvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now - EvaluationWindow;

        var messages = await dbContext.Messages.AsNoTracking()
            .Where(x => x.CreatedAt >= windowStart && x.CreatedAt <= now)
            .OrderBy(x => x.ConversationId)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new MessageSlice(
                x.ConversationId,
                x.SenderId,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var replyMinutesByUser = BuildReplyMinutesByUser(messages);
        var users = await dbContext.AspNetUsers.ToListAsync(cancellationToken);

        var updatedCount = 0;
        var quickResponderCount = 0;
        foreach (var user in users)
        {
            replyMinutesByUser.TryGetValue(user.Id, out var replyMinutes);
            int? p75Minutes = replyMinutes is { Count: > 0 }
                ? CalculateP75Minutes(replyMinutes)
                : null;
            var isQuickResponder = p75Minutes.HasValue && p75Minutes.Value < QuickResponderThresholdMinutes;

            if (user.IsQuickResponder != isQuickResponder || user.QuickResponderP75Minutes != p75Minutes)
            {
                updatedCount += 1;
            }

            user.IsQuickResponder = isQuickResponder;
            user.QuickResponderP75Minutes = p75Minutes;
            user.QuickResponderEvaluatedAt = now;

            if (isQuickResponder)
            {
                quickResponderCount += 1;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Quick responder evaluation finished. Users={TotalUsers}, Updated={UpdatedUsers}, Qualified={QualifiedUsers}",
            users.Count,
            updatedCount,
            quickResponderCount);

        return new QuickResponderEvaluationResult(users.Count, updatedCount, quickResponderCount);
    }

    private Dictionary<string, List<double>> BuildReplyMinutesByUser(IReadOnlyList<MessageSlice> messages)
    {
        var replyMinutesByUser = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        MessageSlice? previousMessage = null;

        foreach (var currentMessage in messages)
        {
            if (previousMessage is null ||
                previousMessage.Value.ConversationId != currentMessage.ConversationId)
            {
                previousMessage = currentMessage;
                continue;
            }

            if (currentMessage.SenderId != previousMessage.Value.SenderId)
            {
                var replyDelay = currentMessage.CreatedAt - previousMessage.Value.CreatedAt;
                if (replyDelay > TimeSpan.Zero && replyDelay <= MaxReasonableReplyDelay)
                {
                    if (!replyMinutesByUser.TryGetValue(currentMessage.SenderId, out var durations))
                    {
                        durations = [];
                        replyMinutesByUser[currentMessage.SenderId] = durations;
                    }

                    durations.Add(replyDelay.TotalMinutes);
                }
            }

            previousMessage = currentMessage;
        }

        return replyMinutesByUser;
    }

    private static int CalculateP75Minutes(List<double> values)
    {
        values.Sort();
        var index = (int)Math.Ceiling(values.Count * 0.75d) - 1;
        index = Math.Clamp(index, 0, values.Count - 1);
        return (int)Math.Round(values[index], MidpointRounding.AwayFromZero);
    }

    private readonly record struct MessageSlice(
        Guid ConversationId,
        string SenderId,
        DateTime CreatedAt);
}

public sealed record QuickResponderEvaluationResult(
    int TotalUsers,
    int UpdatedUsers,
    int QualifiedUsers);
