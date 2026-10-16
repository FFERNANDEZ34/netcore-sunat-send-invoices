using FluentValidation;

namespace Core.Application.Features.Invoices.Commands
{
    public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
    {
        public CreateInvoiceCommandValidator()
        {
            // Validación de Cabecera básica
            RuleFor(x => x.Serie)
                .NotEmpty().WithMessage("La serie es obligatoria.")
                .Matches(@"^[FB][A-Z0-9]{3}$").WithMessage("La serie debe empezar con F o B y tener 4 caracteres (Ej: F001, B020).");

            RuleFor(x => x.Correlativo)
                .GreaterThan(0).WithMessage("El correlativo debe ser un número entero mayor a cero.");

            RuleFor(x => x.TipoComprobante)
                .Must(t => t == "01" || t == "03").WithMessage("Solo se permite tipo 01 (Factura) o 03 (Boleta).");

            // Validación del Emisor
            RuleFor(x => x.Emisor).NotNull().WithMessage("Los datos del emisor de tu empresa son obligatorios.");
            RuleFor(x => x.Emisor.Ruc).Length(11).Matches(@"^\d{11}$").WithMessage("El RUC del emisor debe tener exactamente 11 dígitos numéricos.");

            // Validación del Cliente Receptor
            RuleFor(x => x.Receptor).NotNull().WithMessage("Los datos del cliente receptor son obligatorios.");
            RuleFor(x => x.Receptor.NumeroDocumento).NotEmpty().WithMessage("El número de documento del cliente es obligatorio.");

            // Validación del Detalle
            RuleFor(x => x.Lineas).NotEmpty().WithMessage("El comprobante debe contener al menos un ítem en el detalle.");

            RuleForEach(x => x.Lineas).ChildRules(linea =>
            {
                linea.RuleFor(l => l.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
                linea.RuleFor(l => l.TipoAfectacion).NotEmpty().WithMessage("El tipo de afectación IGV es obligatorio (Ej: 10).");

                // Redondeo estricto por ítem para evitar fugas de centavos
                linea.RuleFor(l => l.TotalLinea)
                    .Equal(l => Math.Round(l.Cantidad * l.ValorUnitario, 4))
                    .WithMessage(l => $"Línea {l.Id}: El total informado ({l.TotalLinea}) no cuadra matemáticamente con Cantidad * ValorUnitario.");
            });

            // 🎯 BLINDAJE DE TOTALES GLOBALES: Previene el error de rechazo de SUNAT por descuadres de 0.01 centavos
            RuleFor(x => x)
                .Must(x => Math.Round(x.TotalGravado + x.TotalIgv, 2) == x.TotalPagar)
                .WithMessage("Descuadre crítico de totales: La suma de Total Gravado + Total IGV debe dar exactamente el Total a Pagar.");
        }
    }
}
