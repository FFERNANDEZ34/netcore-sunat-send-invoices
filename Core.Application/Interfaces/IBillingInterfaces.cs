using Core.Application.Features.Invoices.Commands;


namespace Core.Application.Interfaces
{
    public interface IXmlGenerator
    {
        string GenerateInvoiceXml(CreateInvoiceCommand command);
    }

    public interface ICryptoSigner
    {
        Task<(string SignedXml, string XmlHash)> SignXmlAsync(string xmlRaw, string rucEmisor, string certificadoUrl, string certificadoClave);

    }

    public interface IZipService
    {
        byte[] CompressToZip(string xmlContent, string fileName);
        CdrInfoResponse ExtractCdrInfo(byte[] cdrZipBytes);
    }

    public interface ISunatSoapClient
    {
        Task<SunatResponseResult> SendBillAsync(byte[] zipBytes, string fileName, string rucEmisor);
    }

    // DTOs de soporte para las interfaces
    public record CdrInfoResponse(string ResponseCode, string Description, List<string> Observations);
    public record SunatResponseResult(bool Success, byte[]? CdrBytes, string ErrorCode, string ErrorMessage);
}
