import { useQuery } from "@tanstack/react-query";
import { fetchJob, JobStatus } from "../api";

const terminalStatuses: JobStatus[] = ["Completed", "Failed"];

export function useJobPolling(jobId?: string) {
  return useQuery({
    queryKey: ["job", jobId],
    queryFn: () => fetchJob(jobId!),
    enabled: Boolean(jobId),
    refetchInterval: (data) => {
      if (!data || terminalStatuses.includes(data.status)) return false;
      return 2500;
    }
  });
}
