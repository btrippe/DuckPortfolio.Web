using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;

namespace DuckPortfolio.Web.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/auth-check")]
    public class AuthCheckController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenAcquisition _tokenAcquisition;

        public AuthCheckController(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ITokenAcquisition tokenAcquisition)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _tokenAcquisition = tokenAcquisition;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
        {
            var apiBaseUrl = _configuration["PortfolioApi:BaseUrl"];
            var apiScope = _configuration["PortfolioApi:Scope"];
            var consentUrl = Url.Content("~/account/consent-api?redirectUri=/auth-check");
            const string authScheme = CookieAuthenticationDefaults.AuthenticationScheme;

            if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(apiScope))
            {
                return Problem(
                    title: "Portfolio API settings are incomplete.",
                    detail: "Set PortfolioApi:BaseUrl and PortfolioApi:Scope in configuration.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                var accessToken = await HttpContext.GetTokenAsync(authScheme, "access_token");
                var tokenSource = "authentication-session";

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                        new[] { apiScope },
                        authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme,
                        user: User);
                    tokenSource = "token-acquisition";
                }

                var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(new Uri(apiBaseUrl, UriKind.Absolute), "/api/me"));

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var contentType = response.Content.Headers.ContentType?.MediaType;

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = "The signed-in call to the protected API failed.",
                        apiStatus = (int)response.StatusCode,
                        apiUrl = new Uri(new Uri(apiBaseUrl, UriKind.Absolute), "/api/me").ToString(),
                        tokenSource,
                        contentType,
                        detail = responseBody
                    });
                }

                return Content(responseBody, contentType ?? "application/json");
            }
            catch (MicrosoftIdentityWebChallengeUserException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "The user needs to grant or refresh consent for the downstream API scope.",
                    scope = apiScope,
                    consentUrl,
                    detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Problem(
                    title: "The signed-in call failed before the API response could be processed.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("diagnostics")]
        public async Task<IActionResult> GetDiagnostics()
        {
            var apiScope = _configuration["PortfolioApi:Scope"];
            var apiBaseUrl = _configuration["PortfolioApi:BaseUrl"];
            const string authScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var authenticateResult = await HttpContext.AuthenticateAsync(authScheme);
            var sessionAccessToken = await HttpContext.GetTokenAsync(authScheme, "access_token");
            var sessionIdToken = await HttpContext.GetTokenAsync(authScheme, "id_token");
            var tokenAcquisitionDiagnostic = await TryAcquireTokenAsync(apiScope);

            return Ok(new
            {
                isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                authScheme,
                hasAuthenticationTicket = authenticateResult.Succeeded,
                hasAccessToken = !string.IsNullOrWhiteSpace(sessionAccessToken),
                hasIdToken = !string.IsNullOrWhiteSpace(sessionIdToken),
                configuredApiBaseUrl = apiBaseUrl,
                configuredApiScope = apiScope,
                consentUrl = Url.Content("~/account/consent-api?redirectUri=/auth-check"),
                accessToken = DescribeJwt(sessionAccessToken),
                idToken = DescribeJwt(sessionIdToken),
                tokenAcquisition = tokenAcquisitionDiagnostic,
                userClaims = User.Claims.Select(claim => new
                {
                    claim.Type,
                    claim.Value
                })
            });
        }

        private async Task<object> TryAcquireTokenAsync(string? apiScope)
        {
            if (string.IsNullOrWhiteSpace(apiScope))
            {
                return new
                {
                    attempted = false,
                    reason = "PortfolioApi:Scope is not configured."
                };
            }

            try
            {
                var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
                    new[] { apiScope },
                    authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme,
                    user: User);

                return new
                {
                    attempted = true,
                    succeeded = true,
                    token = DescribeJwt(accessToken)
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    attempted = true,
                    succeeded = false,
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message
                };
            }
        }

        private static object DescribeJwt(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new
                {
                    present = false
                };
            }

            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
            {
                return new
                {
                    present = true,
                    readable = false
                };
            }

            var jwt = handler.ReadJwtToken(token);

            return new
            {
                present = true,
                readable = true,
                audience = jwt.Audiences.ToArray(),
                scopes = jwt.Claims.Where(claim => claim.Type == "scp").Select(claim => claim.Value).ToArray(),
                roles = jwt.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value).ToArray(),
                issuer = jwt.Issuer,
                expiresAtUtc = jwt.ValidTo,
                subject = jwt.Subject
            };
        }
    }
}
