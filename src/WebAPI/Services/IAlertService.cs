using System.Collections.Generic;
using System.Threading.Tasks;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Hubs;

namespace SuperPanel.WebAPI.Services;

public interface IAlertService
{
    // Alert Rules Management
    Task<IEnumerable<AlertRule>> GetAllAlertRulesAsync();
    Task<AlertRule> GetAlertRuleByIdAsync(int id);
    Task<AlertRule> CreateAlertRuleAsync(AlertRule rule);
    Task<AlertRule> UpdateAlertRuleAsync(int id, AlertRule updatedRule);
    Task DeleteAlertRuleAsync(int id);

    // Alert Management
    Task<IEnumerable<Alert>> GetAllAlertsAsync(int? serverId = null, AlertStatus? status = null);
    Task<Alert> GetAlertByIdAsync(int id);
    Task<Alert> CreateAlertAsync(Alert alert);
    Task<Alert> AcknowledgeAlertAsync(int id);
    Task<Alert> ResolveAlertAsync(int id);

    // Alert History and Comments
    Task AddAlertHistoryAsync(int alertId, string action, AlertStatus oldStatus, AlertStatus newStatus, string description = null, string performedBy = "System");
    Task<IEnumerable<AlertHistory>> GetAlertHistoryAsync(int alertId);
    Task<AlertComment> AddAlertCommentAsync(int alertId, string comment, string commentType = "General", string createdBy = "System");
    Task<IEnumerable<AlertComment>> GetAlertCommentsAsync(int alertId);
    Task AcknowledgeAlertWithCommentAsync(int alertId, string comment = null, string acknowledgedBy = "System");
    Task ResolveAlertWithCommentAsync(int alertId, string comment = null, string resolvedBy = "System");

    // Alert Evaluation
    Task EvaluateAlertRulesAsync();
    Task EvaluateAlertRulesAsync(Server server, ServerMetrics metrics);

    // Testing
    Task TestAlertRuleAsync(int alertRuleId);

    // Statistics
    Task<AlertStats> GetAlertStatsAsync();
}