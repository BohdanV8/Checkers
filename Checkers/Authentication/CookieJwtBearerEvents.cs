using Microsoft.AspNetCore.Authentication.JwtBearer; 

namespace Checkers.Authentication
{
    public class CookieJwtBearerEvents : JwtBearerEvents
    {
        public override Task MessageReceived(MessageReceivedContext context)
        {
            // Try to get token from cookie
            var accessToken = context.Request.Cookies["accessToken"];

            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    }
}
