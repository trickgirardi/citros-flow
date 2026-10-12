const MISSING_ENV_ERROR =
  "Supabase environment variables are missing. Configure NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY.";

function getRequiredEnv(name: string): string {
  const value = process.env[name];

  if (!value) {
    throw new Error(`${MISSING_ENV_ERROR} Missing: ${name}`);
  }

  return value;
}

function getSupabasePublicKey() {
  return (
    process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY ??
    process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY ??
    ""
  );
}

export function getSupabasePublicEnv() {
  const publicKey = getSupabasePublicKey();

  if (!publicKey) {
    throw new Error(
      `${MISSING_ENV_ERROR} Missing: NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`
    );
  }

  return {
    url: getRequiredEnv("NEXT_PUBLIC_SUPABASE_URL"),
    anonKey: publicKey,
  };
}

export function getSupabaseServiceRoleKey() {
  return getRequiredEnv("SUPABASE_SERVICE_ROLE_KEY");
}
