using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Application.Interfaces
{
    public interface IStorageService
    {
        // Sube cualquier archivo binario a Cloudflare R2 de forma asíncrona
        Task<string> UploadFileAsync(byte[] fileBytes, string destinationUrl, string contentType);
    }
}
