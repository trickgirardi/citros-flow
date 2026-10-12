using Financas.Api.Features.Transacoes;

namespace Financas.Api.Tests;

public class NubankCsvParserTests
{
    private const string Header = "Data,Valor,Identificador,Descrição";

    private static List<ImportedTransaction> Ok(string csv)
    {
        var (transactions, error) = NubankCsvParser.Parse(csv);
        Assert.Null(error);
        return transactions!;
    }

    private static string Error(string csv)
    {
        var (transactions, error) = NubankCsvParser.Parse(csv);
        Assert.Null(transactions);
        return error!;
    }

    [Fact]
    public void Sinal_define_entrada_ou_saida_e_valor_fica_em_modulo()
    {
        var rows = Ok($"{Header}\n01/05/2026,180.00,id1,Pix recebido\n02/05/2026,-45.90,id2,Compra no débito");

        Assert.Equal(
            [(TransactionType.Entrada, 180.00m, new DateOnly(2026, 5, 1), "Pix recebido"),
             (TransactionType.Saida, 45.90m, new DateOnly(2026, 5, 2), "Compra no débito")],
            rows.Select(r => (r.Type, r.Amount, r.Date, r.Description)));
    }

    [Fact]
    public void Identificador_e_ignorado_e_cabecalho_ignora_maiusculas()
    {
        var rows = Ok("DATA,VALOR,IDENTIFICADOR,DESCRIÇÃO\n01/05/2026,10,qualquer-coisa,Descrição");

        Assert.Equal("Descrição", Assert.Single(rows).Description);
    }

    [Fact]
    public void Aceita_bom_crlf_linhas_em_branco_e_espacos()
    {
        var rows = Ok($"﻿{Header}\r\n\r\n  01/05/2026 , 10.50 , id , Uma descrição  \r\n\r\n02/05/2026,-1,id,Outra\r\n");

        Assert.Equal(["Uma descrição", "Outra"], rows.Select(r => r.Description));
        Assert.Equal(10.50m, rows[0].Amount);
    }

    [Fact]
    public void Virgula_decimal_aspas_e_descricao_com_virgula_sem_aspas()
    {
        var csv = Header + "\n"
            + "03/05/2026,\"1234,56\",id,\"Depósito, com vírgula e \"\"aspas\"\"\"\n"
            + "04/05/2026,-7.5,id,Descrição com, vírgula sem aspas";

        var rows = Ok(csv);

        Assert.Equal(1234.56m, rows[0].Amount);
        Assert.Equal("Depósito, com vírgula e \"aspas\"", rows[0].Description);
        Assert.Equal(7.5m, rows[1].Amount);
        // Campos 4+ são juntados de volta. Como cada campo é aparado antes, o espaço depois da vírgula
        // se perde: é o comportamento do importador antigo, mantido de propósito.
        Assert.Equal("Descrição com,vírgula sem aspas", rows[1].Description);
    }

    [Fact]
    public void Valor_e_arredondado_para_centavos_metade_para_longe_do_zero()
    {
        var rows = Ok($"{Header}\n01/05/2026,10.005,id,A\n01/05/2026,-10.004,id,B");

        Assert.Equal([10.01m, 10.00m], rows.Select(r => r.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("Data,Valor,Identificador,Descrição")] // só o cabeçalho
    public void Arquivo_sem_linhas_de_dados_e_vazio(string csv) =>
        Assert.Equal("Arquivo CSV vazio.", Error(csv));

    [Theory]
    [InlineData("Data,Valor,Descrição,Identificador")]
    [InlineData("Data,Valor,Identificador")]
    [InlineData("Date,Amount,Id,Description")]
    public void Cabecalho_diferente_do_esperado_e_recusado(string header) =>
        Assert.Equal("Cabeçalho CSV inválido.", Error($"{header}\n01/05/2026,10,id,Desc"));

    [Theory]
    [InlineData("31/02/2026,10,id,Desc")]   // data inexistente
    [InlineData("2026-05-01,10,id,Desc")]   // formato errado
    [InlineData("1/5/2026,10,id,Desc")]     // sem zero à esquerda
    [InlineData(",10,id,Desc")]             // sem data
    [InlineData("01/05/2026,0,id,Desc")]    // valor zero
    [InlineData("01/05/2026,0.004,id,Desc")] // arredonda para zero
    [InlineData("01/05/2026,abc,id,Desc")]  // valor não numérico
    [InlineData("01/05/2026,,id,Desc")]     // sem valor
    [InlineData("01/05/2026,99999999999,id,Desc")] // estoura numeric(12,2)
    [InlineData("01/05/2026,10,id,")]       // sem descrição
    [InlineData("01/05/2026,10,id")]        // faltam colunas
    public void Linha_invalida_rejeita_o_arquivo_inteiro(string line) =>
        Assert.Equal("Linha 3 inválida.", Error($"{Header}\n01/05/2026,10,id,Linha boa\n{line}"));

    [Fact]
    public void Numero_da_linha_conta_so_as_linhas_nao_vazias()
    {
        // linha em branco no meio não entra na contagem (igual ao importador antigo)
        Assert.Equal("Linha 3 inválida.", Error($"{Header}\n01/05/2026,10,id,Boa\n\n\n32/05/2026,10,id,Ruim"));
    }

    [Fact]
    public void Descricao_maior_que_o_teto_invalida_a_linha()
    {
        Assert.Equal("Linha 2 inválida.", Error($"{Header}\n01/05/2026,10,id,{new string('x', 501)}"));
        Assert.Single(Ok($"{Header}\n01/05/2026,10,id,{new string('x', 500)}"));
    }
}
