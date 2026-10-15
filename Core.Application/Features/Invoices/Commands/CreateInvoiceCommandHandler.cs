using Core.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Application.Features.Invoices.Commands
{
    public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, BillingResponseDto>
    {
        private readonly IXmlGenerator _xmlGenerator;
        private readonly ICryptoSigner _cryptoSigner;
        private readonly ISunatSoapClient _sunatClient;
        private readonly IZipService _zipService;

        public CreateInvoiceCommandHandler(
            IXmlGenerator xmlGenerator,
            ICryptoSigner cryptoSigner,
            ISunatSoapClient sunatClient,
            IZipService zipService)
        {
            _xmlGenerator = xmlGenerator;
            _cryptoSigner = cryptoSigner;
            _sunatClient = sunatClient;
            _zipService = zipService;
        }

        public async Task<BillingResponseDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Convertir el objeto C# a estructura XML UBL 2.1 pura (vía Clases XSD)
                string xmlRaw = _xmlGenerator.GenerateInvoiceXml(request);

                // 2. Firmar digitalmente con el estándar W3C XMLDsig usando URI="" inmutable
                var (xmlSigned, xmlHash) = await _cryptoSigner.SignXmlAsync(xmlRaw, request.Emisor.Ruc, request.Emisor.CertificadoUrl,request.Emisor.CertificadoClave);

                // 3. Comprimir a formato ZIP directamente en memoria (Alto rendimiento)
                string nombreArchivo = $"{request.Emisor.Ruc}-{request.TipoComprobante}-{request.Serie}-{request.Correlativo:D8}";

                // ============================================================
                // 🎯 NUEVO: GUARDADO DE AUDITORÍA LOCAL EN DESARROLLO
                // ============================================================
                try
                {
                    string carpetaAuditoria = Path.Combine(AppContext.BaseDirectory, "XmlAuditoria");
                    if (!Directory.Exists(carpetaAuditoria))
                    {
                        Directory.CreateDirectory(carpetaAuditoria);
                    }

                    string rutaXmlLocal = Path.Combine(carpetaAuditoria, $"{nombreArchivo}.xml");
                    await File.WriteAllTextAsync(rutaXmlLocal, xmlSigned, System.Text.Encoding.UTF8, cancellationToken);

                    Console.WriteLine($"[AUDITORÍA] XML Firmado guardado con éxito en: {rutaXmlLocal}");
                }
                catch (Exception ex)
                {
                    // Un fallo al escribir la auditoría local no debe tumbar la operación de facturación principal
                    Console.WriteLine($"[ADVERTENCIA] No se pudo guardar el XML local de auditoría: {ex.Message}");
                }

                byte[] zipBytes = _zipService.CompressToZip(xmlSigned, nombreArchivo);

                // 4. Enviar los bytes empaquetados al Web Service SOAP de SUNAT (Servidor BETA/PROD)
                var resultadoSunat = await _sunatClient.SendBillAsync(zipBytes, $"{nombreArchivo}.zip", request.Emisor.Ruc);

                // 5. Analizar la Constancia de Recepción (CDR) devuelta por SUNAT
                if (resultadoSunat.Success && resultadoSunat.CdrBytes != null)
                {
                    var cdrInfo = _zipService.ExtractCdrInfo(resultadoSunat.CdrBytes);

                    return new BillingResponseDto(
                        Success: cdrInfo.ResponseCode == "0", // 0 = Aceptado, cualquier otro = Rechazado/Observado
                        SunatResponseCode: cdrInfo.ResponseCode,
                        Message: cdrInfo.Description,
                        XmlHash: xmlHash,
                        CdrXmlBase64: Convert.ToBase64String(resultadoSunat.CdrBytes),
                        Observations: cdrInfo.Observations
                    );
                }

                // Fallo en validación de SUNAT o caída de servidores
                return new BillingResponseDto(
                    Success: false,
                    SunatResponseCode: resultadoSunat.ErrorCode,
                    Message: $"Rechazo o error en comunicación con SUNAT: {resultadoSunat.ErrorMessage}",
                    XmlHash: xmlHash,
                    CdrXmlBase64: string.Empty,
                    Observations: new List<string>()
                );
            }
            catch (Exception ex)
            {
                // Captura de excepciones críticas internas de infraestructura
                return new BillingResponseDto(
                    Success: false,
                    SunatResponseCode: "500",
                    Message: $"Error interno en el microservicio de facturación: {ex.Message}",
                    XmlHash: string.Empty,
                    CdrXmlBase64: string.Empty,
                    Observations: new List<string>()
                );
            }
        }
    }
}
