using Amazon.S3;
using Amazon.S3.Model;
using Core.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;


namespace Infrastructure.Storage
{
    public class CloudflareStorageService : IStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _publicDomain;

        public CloudflareStorageService(IConfiguration configuration)
        {
            var section = configuration.GetSection("CloudflareR2");

            string accountId = section["AccountId"] ?? throw new ArgumentNullException("CloudflareR2:AccountId");
            string accessKey = section["AccessKey"] ?? throw new ArgumentNullException("CloudflareR2:AccessKey");
            string secretKey = section["SecretKey"] ?? throw new ArgumentNullException("CloudflareR2:SecretKey");

            _bucketName = section["BucketName"] ?? throw new ArgumentNullException("CloudflareR2:BucketName");
            _publicDomain = section["PublicCustomDomain"]?.TrimEnd('/') ?? throw new ArgumentNullException("CloudflareR2:PublicCustomDomain");

            // 🎯 Configuración Oficial compatible S3 para Cloudflare R2
            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
        }

        public async Task<string> UploadFileAsync(byte[] fileBytes, string destinationPath, string contentType)
        {
            // 🎯 R2 exige limpiar el prefijo de la URL si se procesa como llave del objeto
            // Limpiamos la ruta para que quede solo: "tenant_23/companies/25/invoices/archivo.xml"
            string objectKey = destinationPath
                .Replace(_publicDomain, "")
                .TrimStart('/');

            using var memoryStream = new MemoryStream(fileBytes);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = memoryStream,
                ContentType = contentType,
                DisablePayloadSigning = true
            };

            // El SDK calcula la firma AWS Signature V4 de forma automática
            var response = await _s3Client.PutObjectAsync(request);

            // Retorna la URL pública final combinada para tu base de datos de Node.js
            return $"{_publicDomain}/{objectKey}";
        }
    }
}
