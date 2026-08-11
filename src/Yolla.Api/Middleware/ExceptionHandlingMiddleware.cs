using Microsoft.AspNetCore.Mvc;
using Yolla.Application.Common;

namespace Yolla.Api.Middleware;

/// <summary>
/// Yakalanmamış hataları RFC 7807 <c>ProblemDetails</c> biçimine çevirir.
/// </summary>
/// <remarks>
/// İstemciye hiçbir zaman yığın izi (stack trace) veya iç hata metni gönderilmez;
/// bunlar sunucu tarafında loglanır. İstisna tipleri anlamlı HTTP kodlarına eşlenir.
/// </remarks>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private const int ClientClosedRequest = 499;


    /// <summary>İsteği işler ve oluşan hataları ProblemDetails yanıtına çevirir.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Yanıt gönderilmeye başlandıktan sonra hata oluştu.");
            return;
        }

        var problem = CreateProblemDetails(context, exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "İşlenmeyen hata: {Path}", context.Request.Path);
        }
        else
        {
            logger.LogWarning("İstek reddedildi ({Status}): {Message}", problem.Status, exception.Message);
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var problem = exception switch
        {
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Kayıt bulunamadı",
                Detail = notFound.Message
            },

            RequestValidationException validation => CreateValidationProblem(validation),

            UpstreamServiceException upstream => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Dış servis yanıt vermedi",
                Detail = $"{upstream.Service} servisine ulaşılamadı, lütfen tekrar deneyin."
            },

            OperationCanceledException => new ProblemDetails
            {
                // 499 standart değil ama istemci bağlantıyı kapattığında yaygın kullanım
                Status = ClientClosedRequest,
                Title = "İstek iptal edildi"
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Beklenmeyen bir hata oluştu",
                // İç hata metni istemciye sızdırılmaz
                Detail = "İstek işlenirken bir sorun oluştu."
            }
        };

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        return problem;
    }

    private static ValidationProblemDetails CreateValidationProblem(RequestValidationException exception)
    {
        var errors = exception.Errors.ToDictionary(x => x.Key, x => x.Value);

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "İstek doğrulanamadı"
        };
    }
}
