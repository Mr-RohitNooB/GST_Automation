using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AutomationServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ModulesController : ControllerBase
    {
        private readonly string _connectionString;

        public ModulesController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        /// <summary>
        /// Returns the latest JSON automation script config for a given module.
        /// The client uses this config to drive all Playwright selectors and URLs.
        /// When the GST portal changes layout, only this DB record needs updating — no DLL rebuild.
        /// </summary>
        [HttpGet("script/{moduleId:int}")]
        public async Task<IActionResult> GetScript(int moduleId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT TOP 1 JsonContent, Version
                    FROM   AutomationScripts
                    WHERE  ModuleId = @ModuleId
                    ORDER  BY ReleaseDate DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ModuleId", moduleId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var jsonContent = reader.GetString(0);
                    var version     = reader.GetString(1);

                    Response.Headers["X-Script-Version"] = version;
                    return Content(jsonContent, "application/json");
                }

                return NotFound(new { Message = $"No automation script found for module {moduleId}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Error fetching script.", Error = ex.Message });
            }
        }
    }
}
