/** Duas iniciais: primeira e última palavra do nome ("Maria da Silva" → "MS"). */
export function initialsOf(name: string) {
  const words = name.trim().split(/\s+/).filter(Boolean)

  if (words.length === 0) {
    return "?"
  }

  const first = words[0][0]
  const last = words.length > 1 ? words[words.length - 1][0] : ""

  return `${first}${last}`.toUpperCase()
}
