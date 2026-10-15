using Core.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Infrastructure.Zip
{
    public class ZipService : IZipService
    {
        // Comprime el XML firmado directamente en un arreglo de bytes en memoria
        public byte[] CompressToZip(string xmlContent, string fileName)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var zipEntry = archive.CreateEntry($"{fileName}.xml", CompressionLevel.Optimal);
                using var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8);
                writer.Write(xmlContent);
            }
            return memoryStream.ToArray();
        }

        // Abre el ZIP del CDR enviado por SUNAT y extrae la información de aceptación
        public CdrInfoResponse ExtractCdrInfo(byte[] cdrZipBytes)
        {
            using var memoryStream = new MemoryStream(cdrZipBytes);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            // El CDR siempre contiene un único archivo XML dentro
            var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                return new CdrInfoResponse("999", "El archivo CDR retornado por SUNAT no contiene un XML válido.", new List<string>());
            }

            using var entryStream = entry.Open();
            var xmlDoc = XDocument.Load(entryStream);

            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

            // Localizar el nodo de respuesta de SUNAT dentro del CDR
            var documentResponse = xmlDoc.Descendants(cac + "DocumentResponse").FirstOrDefault();
            var response = documentResponse?.Element(cac + "Response");

            string responseCode = response?.Element(cbc + "ResponseCode")?.Value ?? "999";
            string description = response?.Element(cbc + "Description")?.Value ?? "No se encontró descripción en el CDR.";

            // Extraer observaciones si existieran (Advertencias de SUNAT que no rechazan el comprobante)
            var observations = response?.Elements(cbc + "Note")
                                        .Select(n => n.Value)
                                        .ToList() ?? new List<string>();

            return new CdrInfoResponse(responseCode, description, observations);
        }
    }
}
