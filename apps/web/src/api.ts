export type SummaryLane = "LaneA" | "LaneB" | "LaneC" | "LaneD" | "LaneE";
export type JobStatus = "Queued" | "Processing" | "Completed" | "Failed";
export type Audience = "Patients" | "General public" | "Healthcare professionals";

export type EntitlementFeature = {
  images?: boolean;
  citations?: boolean;
};

export type EntitlementsResponse = {
  enabledLanes: SummaryLane[];
  features?: EntitlementFeature;
};

export type JobResponse = {
  jobId: string;
  status: JobStatus;
  summaryId?: string;
  errorMessage?: string | null;
};

type RuntimeConfig = {
  apiBaseUrl?: string;
};

let runtimeConfig: RuntimeConfig | null = null;
let runtimeConfigPromise: Promise<RuntimeConfig> | null = null;

async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  if (runtimeConfig) return runtimeConfig;
  if (!runtimeConfigPromise) {
    runtimeConfigPromise = fetch("/config.json", { cache: "no-store" })
      .then((response) => (response.ok ? response.json() : {}))
      .catch(() => ({}))
      .then((config) => {
        runtimeConfig = config as RuntimeConfig;
        return runtimeConfig;
      });
  }
  return runtimeConfigPromise;
}

async function getApiBase(): Promise<string> {
  const config = await loadRuntimeConfig();
  return config.apiBaseUrl ?? import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5072";
}

function authHeaders(): HeadersInit {
  const token = localStorage.getItem("pls_token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function devLogin(email: string, tenantName: string) {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/auth/dev-login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, tenantName })
  });
  if (!response.ok) {
    throw new Error("Login failed");
  }
  return response.json();
}

export async function fetchEntitlements(): Promise<EntitlementsResponse> {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/entitlements`, {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Failed to load entitlements");
  }
  const data = await response.json();
  if (Array.isArray(data)) {
    return { enabledLanes: data as SummaryLane[] };
  }
  return {
    enabledLanes: (data.enabledLanes ?? []) as SummaryLane[],
    features: data.features
  };
}

export async function uploadDocument(
  file: File,
  lane: SummaryLane,
  promptOverride?: string,
  metadata?: { title?: string; therapeuticArea?: string; audience?: Audience }
) {
  const apiBase = await getApiBase();
  const form = new FormData();
  form.append("file", file);
  if (metadata?.title) form.append("title", metadata.title);
  if (metadata?.therapeuticArea) form.append("therapeuticArea", metadata.therapeuticArea);
  if (metadata?.audience) form.append("audience", metadata.audience);
  if (promptOverride) {
    form.append("promptOverride", promptOverride);
  }
  const response = await fetch(`${apiBase}/api/documents/upload?lane=${lane}`, {
    method: "POST",
    headers: { ...authHeaders() },
    body: form
  });
  if (!response.ok) {
    throw new Error("Upload failed");
  }
  return response.json();
}

export async function fetchPromptTemplate(lane: SummaryLane) {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/prompts/${lane}`, {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Failed to fetch prompt template");
  }
  return response.text();
}

export async function generateSummary(documentId: string, lane: SummaryLane) {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/summaries/${documentId}/generate?lane=${lane}`, {
    method: "POST",
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Generate failed");
  }
  return response.json();
}

export async function fetchJob(jobId: string): Promise<JobResponse> {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/jobs/${jobId}`, {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Job fetch failed");
  }
  return response.json() as Promise<JobResponse>;
}

export async function fetchSummaries(params: {
  search?: string;
  lane?: SummaryLane;
  status?: JobStatus;
}) {
  const apiBase = await getApiBase();
  const url = new URL(`${apiBase}/api/summaries`);
  if (params.search) url.searchParams.set("search", params.search);
  if (params.lane) url.searchParams.set("lane", params.lane);
  if (params.status) url.searchParams.set("status", params.status);

  const response = await fetch(url.toString(), {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Failed to fetch summaries");
  }
  return response.json();
}

export async function fetchSummary(summaryId: string) {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/summaries/${summaryId}`, {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Failed to fetch summary");
  }
  return response.json();
}

export async function downloadSummary(summaryId: string, format: "pdf" | "docx") {
  const apiBase = await getApiBase();
  const response = await fetch(`${apiBase}/api/summaries/${summaryId}/download?format=${format}`, {
    headers: { ...authHeaders() }
  });
  if (!response.ok) {
    throw new Error("Failed to download summary");
  }

  const blob = await response.blob();
  const filename = getFilenameFromDisposition(response.headers.get("content-disposition"))
    ?? `summary-${summaryId}.${format}`;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function getFilenameFromDisposition(disposition: string | null) {
  if (!disposition) return null;
  const match = disposition.match(/filename=\"?([^\";]+)\"?/i);
  return match?.[1] ?? null;
}
