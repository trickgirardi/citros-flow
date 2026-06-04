import type { Database } from "@/types/database";

export type Transaction = Database["public"]["Tables"]["transactions"]["Row"];
export type TransactionType = Transaction["type"];

export type TransactionGroup = {
  category: string;
  total: number;
  transactions: Transaction[];
};

const currencyFormatter = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
});

const dateFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
});

export function formatCurrency(value: number) {
  return currencyFormatter.format(value);
}

export function formatTransactionDate(value: string) {
  return dateFormatter.format(new Date(`${value}T00:00:00`));
}

export function filterTransactionsByType(
  transactions: Transaction[],
  type: TransactionType
) {
  return transactions.filter((transaction) => transaction.type === type);
}

export function groupTransactionsByCategory(transactions: Transaction[]) {
  const groups = new Map<string, TransactionGroup>();

  transactions.forEach((transaction) => {
    const currentGroup = groups.get(transaction.category) ?? {
      category: transaction.category,
      total: 0,
      transactions: [],
    };

    currentGroup.total += transaction.amount;
    currentGroup.transactions.push(transaction);
    groups.set(transaction.category, currentGroup);
  });

  return Array.from(groups.values()).sort((first, second) =>
    first.category.localeCompare(second.category, "pt-BR")
  );
}

export function sumTransactions(transactions: Transaction[]) {
  return transactions.reduce((total, transaction) => total + transaction.amount, 0);
}
