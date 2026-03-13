namespace AutomationServer.Api.Models
{
    #region Data Transfer Objects (DTOs)
    // Context: DTOs are lightweight classes used ONLY for passing data over the network.

    /// <summary>
    /// Represents the incoming JSON payload from the WPF App when a user tries to log in.
    /// </summary>
    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
    /// <summary>
    /// Represents the incoming JSON payload for registering a new user.
    /// </summary>
    public class RegisterRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
    }
    /// <summary>
    /// Represents the outgoing JSON payload we send back upon a successful login.
    /// </summary>
    public class LoginResponse
    {
        public required string Token { get; set; }
        public Guid SessionId { get; set; }
        public required string Message { get; set; }
    }
    #endregion
}