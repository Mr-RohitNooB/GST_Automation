using AutomationServer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AutomationServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;

        #region Constructor & Dependency Injection
        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Connection string not found.");
        }
        #endregion

        #region Register Endpoint
        // Context: Listens for POST requests at /api/auth/register.
        // This takes a raw password, hashes it with BCrypt, and inserts the user into the database.
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Context: BCrypt automatically generates a salt and hashes the password.
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Check if the email already exists
                    string checkEmailQuery = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
                    using (SqlCommand checkCmd = new SqlCommand(checkEmailQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", request.Email);
                        int exists = (int)await checkCmd.ExecuteScalarAsync();
                        if (exists > 0)
                        {
                            return BadRequest(new { Message = "User with this email already exists." });
                        }
                    }

                    // Insert the user with the securely HASHED password
                    string insertQuery = "INSERT INTO Users (Email, PasswordHash, FullName, IsActive) VALUES (@Email, @PasswordHash, @FullName, 1)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Email", request.Email);
                        insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        insertCmd.Parameters.AddWithValue("@FullName", request.FullName);

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { Message = "User registered successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred during registration.", Error = ex.Message });
            }
        }
        #endregion

        #region Login Endpoint
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            Guid sessionId = Guid.Empty;
            int loginStatus = 0;
            int userId = 0;
            string storedHash = string.Empty;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    #region 1. Verify Password Hash
                    // Context: Fetch the stored hash from the DB first so BCrypt can compare it
                    using (SqlCommand getHashCmd = new SqlCommand("SELECT UserId, PasswordHash FROM Users WHERE Email = @Email AND IsActive = 1", conn))
                    {
                        getHashCmd.Parameters.AddWithValue("@Email", request.Email);
                        using (SqlDataReader reader = await getHashCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                userId = reader.GetInt32(0);
                                storedHash = reader.GetString(1);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(storedHash))
                        return Unauthorized(new { Message = "Invalid email or password." });

                    // Context: Verify the password using BCrypt
                    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, storedHash);

                    if (!isPasswordValid)
                        return Unauthorized(new { Message = "Invalid email or password." });
                    #endregion

                    #region 2. Validate Concurrency Limit
                    // Context: Calling the Stored Procedure to check the 5-device limit.
                    using (SqlCommand cmd = new SqlCommand("sp_UserLoginAndSessionCheck", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Email", request.Email);

                        SqlParameter sessionIdParam = new SqlParameter("@NewSessionId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
                        SqlParameter statusParam = new SqlParameter("@LoginStatus", SqlDbType.Int) { Direction = ParameterDirection.Output };

                        cmd.Parameters.Add(sessionIdParam);
                        cmd.Parameters.Add(statusParam);

                        await cmd.ExecuteNonQueryAsync();

                        loginStatus = (int)statusParam.Value;
                        if (loginStatus == 1)
                        {
                            sessionId = (Guid)sessionIdParam.Value;
                        }
                    }

                    if (loginStatus == -1)
                        return Unauthorized(new { Message = "Maximum simultaneous sessions (5) reached. Please log out of another device." });
                    #endregion

                    #region 3. Fetch Allowed Modules via Subscription
                    List<int> allowedModules = new List<int>();
                    using (SqlCommand getModulesCmd = new SqlCommand("sp_GetAllowedModulesForUser", conn))
                    {
                        getModulesCmd.CommandType = CommandType.StoredProcedure;
                        getModulesCmd.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = await getModulesCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                allowedModules.Add(reader.GetInt32(0));
                            }
                        }
                    }
                    #endregion

                    #region 4. Generate & Return JWT Token
                    string token = GenerateJwtToken(request.Email, sessionId, allowedModules);

                    return Ok(new LoginResponse
                    {
                        Token = token,
                        SessionId = sessionId,
                        Message = "Login Successful"
                    });
                    #endregion
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An internal server error occurred.", Error = ex.Message });
            }
        }
        #endregion

        #region Helper Methods
        private string GenerateJwtToken(string email, Guid sessionId, List<int> allowedModules)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim("session_id", sessionId.ToString()),
                new Claim("allowed_modules", JsonSerializer.Serialize(allowedModules))
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        #endregion
    }
}