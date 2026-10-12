using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Financas.Api.Features.Compartilhamento;

public static class ShareToken
{
    /// <summary>32 bytes aleatórios em base64url (43 caracteres).</summary>
    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 em hex minúsculo. Só isso vai para o banco; o token cru nunca é salvo.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
