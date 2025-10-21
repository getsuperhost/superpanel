using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperPanel.WebAPI.Models;
using SuperPanel.WebAPI.Services;
using System.Security.Claims;

namespace SuperPanel.WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlertRulesController : ControllerBase
    {
        private readonly IAlertService _alertService;

        public AlertRulesController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token");
            }
            return userId;
        }

        private bool IsAdministrator()
        {
            return User.IsInRole("Administrator");
        }

        // GET: api/AlertRules
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AlertRule>>> GetAlertRules()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var isAdmin = IsAdministrator();
                Console.WriteLine($"Getting alert rules for user {currentUserId}, isAdmin: {isAdmin}");
                
                var alertRules = await _alertService.GetAllAlertRulesAsync();
                Console.WriteLine($"Found {alertRules.Count()} total alert rules");
                
                // Filter alert rules by user ownership unless user is admin
                if (!isAdmin)
                {
                    alertRules = alertRules.Where(r => r.UserId == currentUserId).ToList();
                    Console.WriteLine($"Filtered to {alertRules.Count()} rules for user {currentUserId}");
                }
                
                return Ok(alertRules);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAlertRules: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while retrieving alert rules", error = ex.Message });
            }
        }

        // GET: api/AlertRules/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<AlertRule>> GetAlertRule(int id)
        {
            try
            {
                var alertRule = await _alertService.GetAlertRuleByIdAsync(id);
                return Ok(alertRule);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the alert rule", error = ex.Message });
            }
        }

        // POST: api/AlertRules
        [HttpPost]
        public async Task<ActionResult<AlertRule>> CreateAlertRule([FromBody] AlertRule alertRule)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var createdRule = await _alertService.CreateAlertRuleAsync(alertRule);
                return CreatedAtAction(nameof(GetAlertRule), new { id = createdRule.Id }, createdRule);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the alert rule", error = ex.Message });
            }
        }

        // PUT: api/AlertRules/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAlertRule(int id, [FromBody] AlertRule updatedRule)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var alertRule = await _alertService.UpdateAlertRuleAsync(id, updatedRule);
                return Ok(alertRule);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the alert rule", error = ex.Message });
            }
        }

        // DELETE: api/AlertRules/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlertRule(int id)
        {
            try
            {
                await _alertService.DeleteAlertRuleAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the alert rule", error = ex.Message });
            }
        }

        // POST: api/AlertRules/test-create
        [HttpPost("test-create")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateTestAlertRule()
        {
            try
            {
                var testRule = new AlertRule
                {
                    Name = "Test Email Alert",
                    Description = "Test alert rule for email notification testing",
                    ServerId = null, // No specific server
                    MetricName = "CPU",
                    Condition = "gt",
                    Threshold = 80.0,
                    Severity = AlertRuleSeverity.Warning,
                    Enabled = true,
                    CooldownMinutes = 5,
                    WebhookUrl = "https://webhook.site/test-webhook",
                    EmailRecipients = "[\"test@example.com\"]",
                    SlackWebhookUrl = "https://hooks.slack.com/test-slack",
                    Type = AlertRuleType.HighCpuUsage,
                    UserId = 1 // Admin user
                };

                var createdRule = await _alertService.CreateAlertRuleAsync(testRule);
                
                return Ok(new { 
                    message = "Test alert rule created successfully", 
                    alertRuleId = createdRule.Id,
                    testUrl = $"/api/alertrules/{createdRule.Id}/test"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the test alert rule", error = ex.Message });
            }
        }

        // POST: api/AlertRules/{id}/test
        [HttpPost("{id}/test")]
        [AllowAnonymous]
        public async Task<IActionResult> TestAlertRule(int id)
        {
            try
            {
                Console.WriteLine($"Testing alert rule {id}");
                
                await _alertService.TestAlertRuleAsync(id);

                Console.WriteLine($"Test email sent for alert rule {id}");
                return Ok(new { 
                    message = "Test alert created and notifications sent successfully", 
                    alertRuleId = id
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = $"Alert rule with ID {id} not found" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing alert rule {id}: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while testing the alert rule", error = ex.Message });
            }
        }
    }
}