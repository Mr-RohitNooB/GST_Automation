using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AutomationServer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region 1. Add Services to the Container
            // Context: The "Container" handles Dependency Injection. 
            // We are adding the Controllers service so the app knows how to route HTTP requests.
            builder.Services.AddControllers();

            // Context: Swagger is the UI we will use to test our API from the browser.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            #endregion

            #region 2. Configure JWT Authentication
            // Context: We extract the JWT settings from appsettings.json
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];

            // Context: Here we tell .NET to intercept incoming HTTP requests and check for a valid JWT token.
            // If a user tries to access a protected URL without a token signed by our SecretKey, they get a 401 Unauthorized error.
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true, // Ensures expired tokens are rejected
                    ValidateIssuerSigningKey = true, // Validates our custom signature
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!))
                };
            });
            #endregion

            var app = builder.Build();

            #region 3. Configure the HTTP Request Pipeline (Middleware)
            // Context: Middleware is a series of gates that every web request must pass through.

            // If we are developing locally, enable the Swagger testing UI
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Force all traffic over secure HTTPS
            app.UseHttpsRedirection();

            // Context: The order of these two is critical. 
            // You must Authenticate (verify WHO the user is) before you Authorize (check WHAT they can do).
            app.UseAuthentication();
            app.UseAuthorization();

            // Maps the incoming URL to the correct Controller file
            app.MapControllers();
            #endregion

            app.Run();
        }
    }
}