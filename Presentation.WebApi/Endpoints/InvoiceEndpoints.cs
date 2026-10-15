using Core.Application.Features.Invoices.Commands;
using FluentValidation;
using MediatR;

namespace Presentation.WebApi.Endpoints
{
    public static class InvoiceEndpoints
    {
        public static void MapInvoiceEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/v1/billing/invoice", async (
           CreateInvoiceCommand command,
           IMediator mediator,
           IValidator<CreateInvoiceCommand> validator) =>
            {
                var validationResult = await validator.ValidateAsync(command);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary());
                }

                var response = await mediator.Send(command);

                return response.Success
                    ? Results.Ok(response)
                    : Results.BadRequest(response);
            })
       .RequireAuthorization()
       .WithTags("Facturación Electrónica");
        }
    }
}
