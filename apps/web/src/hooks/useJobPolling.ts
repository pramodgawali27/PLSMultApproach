import { useQuery } from "@tanstack/react-query";
import { fetchJob, JobResponse, JobStatus } from "../api";

const terminalStatuses: JobStatus[] = ["Completed", "Failed"];

export function useJobPolling(jobId?: string) {
  return useQuery({
    queryKey: ["job", jobId],
    queryFn: () => fetchJob(jobId!),
    enabled: Boolean(jobId),
    refetchInterval: (query) => {
      const data = query.state.data as JobResponse | undefined;
      if (!data || terminalStatuses.includes(data.status)) return false;
      return 2500;
    }
  });
}
