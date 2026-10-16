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
        private readonly IStorageService _storageService;

        public CreateInvoiceCommandHandler(
            IXmlGenerator xmlGenerator,
            ICryptoSigner cryptoSigner,
            ISunatSoapClient sunatClient,
            IZipService zipService,
            IStorageService storageService)
        {
            _xmlGenerator = xmlGenerator;
            _cryptoSigner = cryptoSigner;
            _sunatClient = sunatClient;
            _zipService = zipService;
            _storageService = storageService;
        }

        public async Task<BillingResponseDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Convertir el objeto C# a estructura XML UBL 2.1 pura (vía Clases XSD)
                string xmlRaw = _xmlGenerator.GenerateInvoiceXml(request);

                // 2. Firmar digitalmente con el estándar W3C XMLDsig usando URI="" inmutable
                var (xmlSigned, xmlHash) = await _cryptoSigner.SignXmlAsync(xmlRaw, request.Emisor.Ruc, request.Emisor.CertificadoUrl, request.Emisor.CertificadoClave);

                // 3. Comprimir a formato ZIP directamente en memoria (Alto rendimiento)
                string nombreArchivo = $"{request.Emisor.Ruc}-{request.TipoComprobante}-{request.Serie}-{request.Correlativo:D8}";

                // ============================================================
                // 🎯 NUEVO: GUARDADO DE AUDITORÍA LOCAL EN DESARROLLO
                // ============================================================
                //try
                //{
                //    string carpetaAuditoria = Path.Combine(AppContext.BaseDirectory, "XmlAuditoria");
                //    if (!Directory.Exists(carpetaAuditoria))
                //    {
                //        Directory.CreateDirectory(carpetaAuditoria);
                //    }

                //    string rutaXmlLocal = Path.Combine(carpetaAuditoria, $"{nombreArchivo}.xml");
                //    await File.WriteAllTextAsync(rutaXmlLocal, xmlSigned, System.Text.Encoding.UTF8, cancellationToken);

                //    Console.WriteLine($"[AUDITORÍA] XML Firmado guardado con éxito en: {rutaXmlLocal}");
                //}
                //catch (Exception ex)
                //{
                //    // Un fallo al escribir la auditoría local no debe tumbar la operación de facturación principal
                //    Console.WriteLine($"[ADVERTENCIA] No se pudo guardar el XML local de auditoría: {ex.Message}");
                //}

                byte[] zipBytes = _zipService.CompressToZip(xmlSigned, nombreArchivo);

                // 4. Enviar los bytes empaquetados al Web Service SOAP de SUNAT (Servidor BETA/PROD)
                var resultadoSunat = await _sunatClient.SendBillAsync(zipBytes, $"{nombreArchivo}.zip", request.Emisor.Ruc, request.Emisor.SunatUsuario, request.Emisor.SunatClave);

                // 5. Analizar la Constancia de Recepción (CDR) devuelta por SUNAT
                if (resultadoSunat.Success && resultadoSunat.CdrBytes != null)
                {
                    var cdrInfo = _zipService.ExtractCdrInfo(resultadoSunat.CdrBytes);

                    // Determinamos el éxito real basado EXCLUSIVAMENTE en la SUNAT
                    bool facturaAceptada = cdrInfo.ResponseCode == "0";

                    // Inicializamos las rutas como vacías por si Cloudflare falla
                    string urlFinalXml = string.Empty;
                    string urlFinalCdr = string.Empty;
                    var observacionesProcesamiento = cdrInfo.Observations ?? new List<string>();

                    if (facturaAceptada)
                    {
                        try {
                            // 🎯 CONSTRUCCIÓN DE RUTAS DINÁMICAS EN TU BUCKET DE CLOUDFLARE R2
                            // Reutilizamos la URL base del certificado del emisor para saber en qué carpeta guardar
                            string urlBaseTenant = request.Emisor.CertificadoUrl.Substring(0, request.Emisor.CertificadoUrl.LastIndexOf("/certificates"));
                            string destinoXml = $"{urlBaseTenant}/invoices/{nombreArchivo}.xml";
                            string destinoCdr = $"{urlBaseTenant}/cdrs/R-{nombreArchivo}.zip";

                            // Subida inmediata de los bytes volátiles de la RAM a la Nube
                            byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlSigned);
                            urlFinalXml  = await _storageService.UploadFileAsync(xmlBytes, destinoXml, "text/xml");
                            urlFinalCdr  = await _storageService.UploadFileAsync(resultadoSunat.CdrBytes, destinoCdr, "application/zip");

                        }
                        catch (Exception ex)
                        {
                            urlFinalXml = string.Empty;
                            urlFinalCdr = string.Empty;
                            Console.WriteLine($"[ADVERTENCIA SAAS] El comprobante fue ACEPTADO por SUNAT, pero falló el resguardo en Cloudflare R2: {ex.Message}");
                            observacionesProcesamiento.Add($"[CONTINGENCIA STORAGE] Comprobante aprobado por SUNAT, pero no se pudo resguardar la copia en la nube. Detalle técnico: {ex.Message}");
                        }


                    }

                        
                    return new BillingResponseDto(
                        Success: facturaAceptada,
                        SunatResponseCode: cdrInfo.ResponseCode,
                        Message: cdrInfo.Description,
                        XmlHash: xmlHash,
                        XmlUrl: urlFinalXml, // 🎯 URL lista para Node.js
                        CdrUrl: urlFinalCdr, // 🎯 URL lista para Node.js
                        Observations: observacionesProcesamiento
                    );
                }

                // Fallo en validación de SUNAT o caída de servidores
                return new BillingResponseDto(
                    Success: false,
                    SunatResponseCode: resultadoSunat.ErrorCode,
                    Message: $"Rechazo o error en comunicación con SUNAT: {resultadoSunat.ErrorMessage}",
                    XmlHash: xmlHash,
                    XmlUrl: string.Empty,
                    CdrUrl:string.Empty,
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
                    XmlUrl: string.Empty,
                    CdrUrl: string.Empty,
                    Observations: new List<string>()
                );
            }
        }
    }
}
