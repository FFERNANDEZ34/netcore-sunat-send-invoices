using MediatR;

namespace Core.Application.Features.Invoices.Commands
{
    // 1. DTO de respuesta unificada del Microservicio
    public record BillingResponseDto(
        bool Success,
        string SunatResponseCode,
        string Message,
        string XmlHash,
        string XmlUrl,      
        string CdrUrl,      
        List<string> Observations
    );

    // 2. El Command que MediatR recibirá y procesará
    public record CreateInvoiceCommand : IRequest<BillingResponseDto>
    {
        public string Serie { get; init; } = string.Empty;
        public int Correlativo { get; init; }
        public string TipoComprobante { get; init; } = "01"; // 01: Factura, 03: Boleta
        public DateTime FechaEmision { get; init; }
        public string Moneda { get; init; } = "PEN";
        public string FormaPago { get; init; } = "Contado";
        public EmisorDto Emisor { get; init; } = null!;
        public ReceptorDto Receptor { get; init; } = null!;

        // Totales Globales de Cabecera
        public decimal TotalGravado { get; init; }
        public decimal TotalIgv { get; init; }
        public decimal TotalPagar { get; init; }

        public List<InvoiceLineDto> Lineas { get; init; } = new();
    }

    public record EmisorDto(string Ruc, string RazonSocial, string Ubigeo, string Direccion, string CertificadoUrl,   // 👉 URL pública o pre-firmada de Cloudflare R2
    string CertificadoClave, string SunatUsuario,
    string SunatClave);
    public record ReceptorDto(string TipoDocumento, string NumeroDocumento, string RazonSocial); // 6=RUC, 1=DNI

    public record InvoiceLineDto(
        int Id,
        decimal Cantidad,
        string UnidadMedida, // NIU = Productos, ZZ = Servicios
        string Descripcion,
        decimal ValorUnitario, // Base Imponible (Sin Impuesto)
        decimal PrecioUnitario, // Comercial (Con Impuesto)
        decimal Igv,
        decimal TotalLinea, // Cantidad * ValorUnitario
        string TipoAfectacion // 10 = Gravado Operación Onerosa
    );
}
