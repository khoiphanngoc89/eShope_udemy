using System.Net;
using BuildingBlock.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Extensions;

public static class EndpointExceptionExtension
{
    public static RouteHandlerBuilder WithException(this RouteHandlerBuilder builder, Func<Exception, IResult?>? func = null)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var result = func?.Invoke(ex);
                if (result is not null)
                {
                    return result;
                }

                return Results.Problem(ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
            }
        });
    }
}