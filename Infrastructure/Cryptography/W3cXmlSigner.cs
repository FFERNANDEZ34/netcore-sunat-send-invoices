using Core.Application.Interfaces;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Infrastructure.Cryptography
{
    public class W3cXmlSigner : ICryptoSigner
    {
        private readonly HttpClient _httpClient;

        public W3cXmlSigner(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // En producción, es recomendable inyectar un servicio que obtenga el PFX desde Azure Key Vault o disco protegido
        public async Task<(string SignedXml, string XmlHash)> SignXmlAsync(string xmlRaw, string rucEmisor, string certificadoUrl, string certificadoClave)
        {
            return await Task.Run(async () =>
            {
                // 1. Cargar el XML en un DOM inmutable
                var xmlDoc = new XmlDocument
                {
                    PreserveWhitespace = true // 🎯 CRÍTICO: Mantener los espacios en blanco exactos del template original
                };
                xmlDoc.LoadXml(xmlRaw);

                // 2. Localizar el nodo de extensión UBL donde exige SUNAT que se incruste la firma
                var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
                namespaceManager.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");

                var extensionContentNode = xmlDoc.SelectSingleNode("//ext:ExtensionContent", namespaceManager) as XmlElement;
                if (extensionContentNode == null)
                {
                    throw new InvalidOperationException("No se encontró el nodo obligatorio <ext:ExtensionContent> en el XML provisto.");
                }
                //**********************************************************************//
                // 🎯 2. DESCARGA VOLÁTIL DESDE CLOUDFLARE R2
                byte[] pfxBytes;
                try
                {
                    // Descarga los bytes del PFX directo a la RAM (Cero escritura en disco duro)
                    // Forzar cabeceras de agente de usuario para evitar bloqueos preventivos de Cloudflare
                    _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    // La línea roja desaparecerá porque este método es "async" y soporta el "await" de manera nativa
                    pfxBytes = await _httpClient.GetByteArrayAsync(certificadoUrl);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error de red al descargar el certificado desde R2: {ex.Message}");
                }

                // 🎯 3. INSTANCIACIÓN EN MEMORIA
                using var certificado = new X509Certificate2(pfxBytes,
                                        certificadoClave,
                                        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet
                                        );

                
                // 4. Inicializar el firmador criptográfico nativo de .NET
                var signedXml = new SignedXml(xmlDoc)
                {
                    SigningKey = certificado.GetRSAPrivateKey() // Extrae de manera segura la llave privada del certificado
                };

                // Configurar algoritmos obligatorios dictados por SUNAT
                signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
                signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";

                var reference = new Reference { Uri = "" }; // URI Vacía obligatoria
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                var keyInfo = new KeyInfo();
                var x509Data = new KeyInfoX509Data(certificado);
                x509Data.AddCertificate(certificado);
                keyInfo.AddClause(x509Data);
                signedXml.KeyInfo = keyInfo;

                signedXml.Signature.Id = $"Signature-{rucEmisor}";

                // 5. Calcular Firma y extraer el hash
                signedXml.ComputeSignature();
                string xmlHash = Convert.ToBase64String(reference.DigestValue ?? Array.Empty<byte>());

                // 6. Inyectar en el nodo correspondiente
                extensionContentNode.AppendChild(xmlDoc.ImportNode(signedXml.GetXml(), true));

                using var stringWriter = new StringWriter();
                using var xmlTextWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { OmitXmlDeclaration = false });
                xmlDoc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();

                return (stringWriter.ToString(), xmlHash);
            });
        }

        
    }
}
