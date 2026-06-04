export type Json =
  | string
  | number
  | boolean
  | null
  | { [key: string]: Json | undefined }
  | Json[];

export type Database = {
  public: {
    Tables: {
      accounts: {
        Row: {
          created_at: string | null;
          id: string;
          name: string;
        };
        Insert: {
          created_at?: string | null;
          id?: string;
          name: string;
        };
        Update: {
          created_at?: string | null;
          id?: string;
          name?: string;
        };
        Relationships: [];
      };
      boards: {
        Row: {
          account_id: string;
          created_at: string | null;
          id: string;
          name: string;
        };
        Insert: {
          account_id: string;
          created_at?: string | null;
          id?: string;
          name: string;
        };
        Update: {
          account_id?: string;
          created_at?: string | null;
          id?: string;
          name?: string;
        };
        Relationships: [
          {
            foreignKeyName: "boards_account_id_fkey";
            columns: ["account_id"];
            isOneToOne: false;
            referencedRelation: "accounts";
            referencedColumns: ["id"];
          },
        ];
      };
      transactions: {
        Row: {
          amount: number;
          board_id: string;
          category: string;
          created_at: string | null;
          created_by: string;
          date: string;
          description: string;
          id: string;
          type: "entrada" | "saida";
        };
        Insert: {
          amount: number;
          board_id: string;
          category: string;
          created_at?: string | null;
          created_by: string;
          date: string;
          description: string;
          id?: string;
          type: "entrada" | "saida";
        };
        Update: {
          amount?: number;
          board_id?: string;
          category?: string;
          created_at?: string | null;
          created_by?: string;
          date?: string;
          description?: string;
          id?: string;
          type?: "entrada" | "saida";
        };
        Relationships: [
          {
            foreignKeyName: "transactions_board_id_fkey";
            columns: ["board_id"];
            isOneToOne: false;
            referencedRelation: "boards";
            referencedColumns: ["id"];
          },
        ];
      };
      user_roles: {
        Row: {
          board_id: string | null;
          id: string;
          role: "admin" | "tesoureiro" | "viewer";
          user_id: string;
        };
        Insert: {
          board_id?: string | null;
          id?: string;
          role: "admin" | "tesoureiro" | "viewer";
          user_id: string;
        };
        Update: {
          board_id?: string | null;
          id?: string;
          role?: "admin" | "tesoureiro" | "viewer";
          user_id?: string;
        };
        Relationships: [
          {
            foreignKeyName: "user_roles_board_id_fkey";
            columns: ["board_id"];
            isOneToOne: false;
            referencedRelation: "boards";
            referencedColumns: ["id"];
          },
        ];
      };
    };
    Views: {
      [_ in never]: never;
    };
    Functions: {
      [_ in never]: never;
    };
    Enums: {
      [_ in never]: never;
    };
    CompositeTypes: {
      [_ in never]: never;
    };
  };
};
