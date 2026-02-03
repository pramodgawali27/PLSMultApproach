import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { devLogin } from "../api";
import { useAuth } from "../components/AuthProvider";

export default function Login() {
  const { setToken } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("demo@pls.local");
  const [tenantName, setTenantName] = useState("Demo Tenant");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const data = await devLogin(email, tenantName);
      setToken(data.accessToken);
      navigate("/app");
    } catch (err) {
      setError("Unable to sign in. Check the dev API is running.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center px-6">
      <div className="glass-panel w-full max-w-lg rounded-3xl p-8">
        <h1 className="font-display text-3xl font-semibold">Welcome back</h1>
        <p className="mt-2 text-sm text-moss">
          Use the dev login to access the demo tenant. Azure AD B2C will replace this in production.
        </p>

        <form className="mt-6 grid gap-4" onSubmit={handleSubmit}>
          <label className="grid gap-1 text-sm font-semibold">
            Email
            <input
              className="rounded-xl border border-clay bg-white/70 px-4 py-2"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              type="email"
              required
            />
          </label>
          <label className="grid gap-1 text-sm font-semibold">
            Tenant name
            <input
              className="rounded-xl border border-clay bg-white/70 px-4 py-2"
              value={tenantName}
              onChange={(event) => setTenantName(event.target.value)}
              required
            />
          </label>
          {error && <p className="text-sm text-ember">{error}</p>}
          <button
            className="rounded-full bg-ink px-6 py-3 font-semibold text-fog"
            type="submit"
            disabled={loading}
          >
            {loading ? "Signing in..." : "Sign in"}
          </button>
        </form>
      </div>
    </div>
  );
}
