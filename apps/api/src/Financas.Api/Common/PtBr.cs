using System.Globalization;

namespace Financas.Api.Common;

public static class PtBr
{
    /// <summary>Ordenação alfabética pt-BR (acentos no lugar certo), como o localeCompare("pt-BR") do front.</summary>
    public static readonly StringComparer Comparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), CompareOptions.None);
}
