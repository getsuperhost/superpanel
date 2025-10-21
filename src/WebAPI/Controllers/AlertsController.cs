using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;

namespace SuperPanel.WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly IAlertService _alertService;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(IAlertService alertService, ILogger<AlertsController> logger)
        {
            _alertService = alertService;
            _logger = logger;
        }

        // GET: api/Alerts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alert>>> GetAlerts(
            [FromQuery] int? serverId = null,
            [FromQuery] AlertStatus? status = null)
        {
            try
            {
                _logger.LogInformation("GetAlerts called with serverId={ServerId}, status={Status}", serverId, status);
                var alerts = await _alertService.GetAllAlertsAsync(serverId, status);
                _logger.LogInformation("Retrieved {Count} alerts", alerts?.Count() ?? 0);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAlerts: {Message}", ex.Message);
                return StatusCode(500, new { message = "An error occurred while retrieving alerts", error = ex.Message });
            }
        }

        // GET: api/Alerts/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Alert>> GetAlert(int id)
        {
            try
            {
                var alert = await _alertService.GetAlertByIdAsync(id);
                return Ok(alert);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the alert", error = ex.Message });
            }
        }

        // POST: api/Alerts
        [HttpPost]
        public async Task<ActionResult<Alert>> CreateAlert([FromBody] Alert alert)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var createdAlert = await _alertService.CreateAlertAsync(alert);
                return CreatedAtAction(nameof(GetAlert), new { id = createdAlert.Id }, createdAlert);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the alert", error = ex.Message });
            }
        }

        // PUT: api/Alerts/{id}/acknowledge
        [HttpPut("{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAlert(int id)
        {
            try
            {
                var alert = await _alertService.AcknowledgeAlertAsync(id);
                return Ok(alert);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while acknowledging the alert", error = ex.Message });
            }
        }

        // PUT: api/Alerts/{id}/resolve
        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> ResolveAlert(int id)
        {
            try
            {
                var alert = await _alertService.ResolveAlertAsync(id);
                return Ok(alert);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while resolving the alert", error = ex.Message });
            }
        }

        // PUT: api/Alerts/{id}/acknowledge-with-comment
        [HttpPut("{id}/acknowledge-with-comment")]
        public async Task<IActionResult> AcknowledgeAlertWithComment(int id, [FromBody] CommentRequest request)
        {
            try
            {
                await _alertService.AcknowledgeAlertWithCommentAsync(id, request?.Comment, request?.User ?? "System");
                return Ok(new { message = "Alert acknowledged with comment" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while acknowledging the alert", error = ex.Message });
            }
        }

        // PUT: api/Alerts/{id}/resolve-with-comment
        [HttpPut("{id}/resolve-with-comment")]
        public async Task<IActionResult> ResolveAlertWithComment(int id, [FromBody] CommentRequest request)
        {
            try
            {
                await _alertService.ResolveAlertWithCommentAsync(id, request?.Comment, request?.User ?? "System");
                return Ok(new { message = "Alert resolved with comment" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while resolving the alert", error = ex.Message });
            }
        }

        // GET: api/Alerts/{id}/history
        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<AlertHistory>>> GetAlertHistory(int id)
        {
            try
            {
                var history = await _alertService.GetAlertHistoryAsync(id);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving alert history", error = ex.Message });
            }
        }

        // GET: api/Alerts/{id}/comments
        [HttpGet("{id}/comments")]
        public async Task<ActionResult<IEnumerable<AlertComment>>> GetAlertComments(int id)
        {
            try
            {
                var comments = await _alertService.GetAlertCommentsAsync(id);
                return Ok(comments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving alert comments", error = ex.Message });
            }
        }

        // POST: api/Alerts/{id}/comments
        [HttpPost("{id}/comments")]
        public async Task<ActionResult<AlertComment>> AddAlertComment(int id, [FromBody] CommentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Comment))
                    return BadRequest(new { message = "Comment is required" });

                var comment = await _alertService.AddAlertCommentAsync(
                    id,
                    request.Comment,
                    request.CommentType ?? "General",
                    request.User ?? "System");

                return CreatedAtAction(nameof(GetAlertComments), new { id = id }, comment);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while adding the comment", error = ex.Message });
            }
        }

        // GET: api/Alerts/stats
        [HttpGet("stats")]
        public async Task<ActionResult<AlertStats>> GetAlertStats()
        {
            try
            {
                var stats = await _alertService.GetAlertStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving alert statistics", error = ex.Message });
            }
        }

        // POST: api/Alerts/evaluate
        [HttpPost("evaluate")]
        public async Task<IActionResult> EvaluateAlertRules()
        {
            try
            {
                await _alertService.EvaluateAlertRulesAsync();
                return Ok(new { message = "Alert rules evaluation completed" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while evaluating alert rules", error = ex.Message });
            }
        }

        // DELETE: api/Alerts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlert(int id)
        {
            try
            {
                // For now, we'll just mark as resolved. In a real system, you might want to actually delete or archive
                var alert = await _alertService.ResolveAlertAsync(id);
                return Ok(new { message = "Alert marked as resolved" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the alert", error = ex.Message });
            }
        }
    }

    public class CommentRequest
    {
        public string Comment { get; set; }
        public string CommentType { get; set; } = "General";
        public string User { get; set; } = "System";
    }
}