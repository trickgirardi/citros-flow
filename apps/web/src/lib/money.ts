// Dinheiro trafega como string decimal ("1234.56"), nunca como float. Aqui ficam só a formatação
// para exibição, a normalização do que o usuário digita e a aritmética em centavos inteiros.

const currencyFormatter = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
})

/** "1234.56" -> "R$ 1.234,56". O Number() é só para exibir: até 9.999.999.999,99 cabe sem perda. */
export function formatMoney(value: string) {
  return currencyFormatter.format(Number(value))
}

/**
 * Normaliza o que o usuário digitou para a string que a API espera.
 * "1.234,56" e "1234,56" viram "1234.56"; "10.5" continua "10.5".
 * Com os dois separadores, o último é o decimal. Devolve null se não for um valor positivo válido.
 */
export function normalizeMoneyInput(input: string) {
  const text = input.replace(/\s/g, "")
  const lastComma = text.lastIndexOf(",")
  const lastDot = text.lastIndexOf(".")

  let normalized = text
  if (lastComma > -1 && lastDot > -1) {
    const decimal = lastComma > lastDot ? "," : "."
    const thousands = decimal === "," ? "." : ","
    normalized = text.split(thousands).join("").replace(decimal, ".")
  } else if (lastComma > -1) {
    normalized = text.replace(",", ".")
  }

  return /^\d+(\.\d+)?$/.test(normalized) ? normalized : null
}

/**
 * "1234.56" -> 123456. Lê os dígitos da string (sem multiplicar float: 1.005 * 100 dá 100.49999...)
 * e arredonda metade para longe do zero pelo terceiro dígito, igual à API. Espera uma string
 * decimal bem formada, como as que a API devolve ou o `normalizeMoneyInput` produz.
 */
export function toCents(value: string) {
  const negative = value.startsWith("-")
  const [whole, fraction = ""] = value.replace("-", "").split(".")
  const digits = fraction.padEnd(3, "0")
  const cents =
    Number(whole) * 100 +
    Number(digits.slice(0, 2)) +
    (Number(digits[2]) >= 5 ? 1 : 0)

  return negative ? -cents : cents
}

/** 123456 -> "1234.56". Aceita negativo ("-0.05"). */
export function fromCents(cents: number) {
  const sign = cents < 0 ? "-" : ""
  const abs = Math.abs(Math.round(cents))
  return `${sign}${Math.floor(abs / 100)}.${String(abs % 100).padStart(2, "0")}`
}

/** 123456 -> "R$ 1.234,56". Para quem faz conta em centavos (ex.: imagem de fechamento). */
export function formatCents(cents: number) {
  return formatMoney(fromCents(cents))
}
