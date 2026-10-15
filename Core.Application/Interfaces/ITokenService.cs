
namespace Core.Application.Interfaces
{
    public interface ITokenService
    {
        // Genera el token JWT para los clientes autorizados a usar tu microservicio
        string GenerateToken(string rucEmpresa, string rol);
    }
}
