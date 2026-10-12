using System.Globalization;
using Financas.Api.Common;

namespace Financas.Api.Features.Transacoes;

public record ImportedTransaction(TransactionType Type, decimal Amount, DateOnly Date, string Description);

/// <summary>
/// Extrato CSV do Nubank: <c>Data,Valor,Identificador,Descrição</c>. Valor positivo é entrada, negativo é
/// saída; o valor é guardado em módulo; "Identificador" é ignorado. Qualquer linha inválida rejeita o arquivo.
/// </summary>
public static class NubankCsvParser
{
    public const string Category = "Dados importados";

    /// <returns>As transações, ou <c>Error</c> com a mensagem para o usuário (nunca os dois).</returns>
    public static (List<ImportedTransaction>? Transactions, string? Error) Parse(string content)
    {
        var lines = content.TrimStart('﻿')
            .Split('\n')
            .Select(line => line.Trim()) // o Trim também remove o \r do CRLF
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count < 2) return (null, "Arquivo CSV vazio.");

        var header = ParseLine(lines[0]).Select(f => f.ToLowerInvariant()).ToList();
        if (header.Count < 4 || header[0] != "data" || header[1] != "valor"
            || header[2] != "identificador" || header[3] != "descrição")
            return (null, "Cabeçalho CSV inválido.");

        var transactions = new List<ImportedTransaction>();
        for (var index = 1; index < lines.Count; index++)
        {
            var fields = ParseLine(lines[index]);
            // Descrição com vírgula sem aspas vira vários campos: junta de volta.
            var description = string.Join(",", fields.Skip(3)).Trim();

            if (!TryParseDate(fields.ElementAtOrDefault(0), out var date)
                || !TryParseAmount(fields.ElementAtOrDefault(1), out var type, out var amount)
                || description.Length is 0 or > TransactionRules.DescriptionMaxLength)
                return (null, $"Linha {index + 1} inválida."); // número = posição entre as linhas não vazias

            transactions.Add(new ImportedTransaction(type, amount, date, description));
        }

        return (transactions, null);
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseAmount(string? value, out TransactionType type, out decimal amount)
    {
        type = default;
        amount = 0;

        var text = value?.Trim().Replace(",", ".") ?? "";
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var signed) || signed == 0)
            return false;
        if (!Money.TryNormalizePositive(Math.Abs(signed), out amount)) return false;

        type = signed > 0 ? TransactionType.Entrada : TransactionType.Saida;
        return true;
    }

    /// <summary>Divide uma linha CSV respeitando aspas ("" dentro de aspas vira "). Campos saem sem espaços nas pontas.</summary>
    internal static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                field.Append('"');
                i++;
            }
            else if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else field.Append(c);
        }

        fields.Add(field.ToString().Trim());
        return fields;
    }
}
