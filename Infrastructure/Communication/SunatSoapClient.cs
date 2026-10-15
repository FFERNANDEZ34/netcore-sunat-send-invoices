using Core.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Net.Http.Headers;

namespace Infrastructure.Communication
{
    public class SunatSoapClient : ISunatSoapClient
    {
        private readonly HttpClient _httpClient;

        public SunatSoapClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<SunatResponseResult> SendBillAsync(byte[] zipBytes, string fileName, string rucEmisor)
        {
            try
            {
                // Url de Homologación / BETA de la SUNAT
                const string urlSunatBeta = "https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService";

                string zipBase64 = Convert.ToBase64String(zipBytes);

                // 🎯 Construcción del sobre SOAP oficial requerido por el Web Service de SUNAT
                var envelope = new StringBuilder();
                envelope.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                envelope.AppendLine("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ser=\"http://service.sunat.gob.pe\" xmlns:wsse=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\">");
                envelope.AppendLine("   <soapenv:Header>");
                envelope.AppendLine("       <wsse:Security>");
                envelope.AppendLine("           <wsse:UsernameToken>");
                envelope.AppendLine($"               <wsse:Username>{rucEmisor}MODDATOS</wsse:Username>"); // Sincronizado automáticamente con el emisor real
                envelope.AppendLine("               <wsse:Password>MODDATOS</wsse:Password>");
                envelope.AppendLine("           </wsse:UsernameToken>");
                envelope.AppendLine("       </wsse:Security>");
                envelope.AppendLine("   </soapenv:Header>");
                envelope.AppendLine("   <soapenv:Body>");
                envelope.AppendLine("      <ser:sendBill>");
                envelope.AppendLine($"         <fileName>{fileName}</fileName>");
                envelope.AppendLine($"         <contentFile>{zipBase64}</contentFile>");
                envelope.AppendLine("      </ser:sendBill>");
                envelope.AppendLine("   </soapenv:Body>");
                envelope.AppendLine("</soapenv:Envelope>");


                using var request = new HttpRequestMessage(HttpMethod.Post, urlSunatBeta);
                request.Content = new StringContent(envelope.ToString(), Encoding.UTF8, "text/xml");
                request.Headers.Clear();
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
                request.Headers.Add("SOAPAction", "urn:sendBill");

                var response = await _httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrEmpty(responseString) && responseString.Contains("Fault"))
                    {
                        var faultDoc = XDocument.Parse(responseString);
                        var faultReason = faultDoc.Descendants().FirstOrDefault(x => x.Name.LocalName == "faultstring")?.Value;
                        var faultCode = faultDoc.Descendants().FirstOrDefault(x => x.Name.LocalName == "faultcode")?.Value;
                        return new SunatResponseResult(false, null, faultCode ?? "500", $"Error Interno SUNAT: {faultReason}");
                    }

                    return new SunatResponseResult(false, null, ((int)response.StatusCode).ToString(), $"Error HTTP del Servidor de SUNAT: {response.ReasonPhrase}");

                }

                // ============================================================
                // 🎯 EXTRACCIÓN ROBUSTA DEL CDR (INMUNE A PREFIJOS DE NAMESPACES)
                // ============================================================
                var xmlDoc = XDocument.Parse(responseString);

                // Buscamos el nodo 'applicationResponse' basándonos únicamente en su nombre local
                var applicationResponseNode = xmlDoc.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName.Equals("applicationResponse", StringComparison.OrdinalIgnoreCase));

                if (applicationResponseNode != null)
                {
                    string cdrBase64 = applicationResponseNode.Value;

                    if (!string.IsNullOrEmpty(cdrBase64))
                    {
                        byte[] cdrBytes = Convert.FromBase64String(cdrBase64);

                        // Retornamos Success = true porque encontramos el archivo binario del CDR
                        return new SunatResponseResult(true, cdrBytes, "0", "Enviado y procesado con éxito.");
                    }
                }

                // Evaluar si la SUNAT devolvió un sobre de falla (SOAP Fault) explicativo
                var faultNode = xmlDoc.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
                if (faultNode != null)
                {
                    string faultCode = faultNode.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("faultcode", StringComparison.OrdinalIgnoreCase))?.Value ?? "FAULT";
                    string faultString = faultNode.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase))?.Value ?? "Error";
                    return new SunatResponseResult(false, null, faultCode, faultString);
                }

                return new SunatResponseResult(false, null, "999", "La SUNAT procesó el archivo pero la etiqueta 'applicationResponse' no fue encontrada en el sobre XML.");
            }
            catch (Exception ex)
            {
                return new SunatResponseResult(false, null, "500", $"Excepción en la conexión de red con SUNAT: {ex.Message}");
            }
        }
    }
}
