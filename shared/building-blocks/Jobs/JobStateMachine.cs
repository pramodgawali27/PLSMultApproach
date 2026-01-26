using PLS.BuildingBlocks.Data;
using PLS.BuildingBlocks.Results;
using PLS.Contracts;

namespace PLS.BuildingBlocks.Jobs;

public static class JobStateMachine
{
    public static Result Start(SummaryJob job)
    {
        if (job.Status != JobStatus.Queued)
        {
            return Result.Fail("Job is not in queued state.");
        }

        job.Status = JobStatus.Processing;
        job.StartedAt = DateTimeOffset.UtcNow;
        return Result.Ok();
    }

    public static Result Complete(SummaryJob job)
    {
        if (job.Status != JobStatus.Processing)
        {
            return Result.Fail("Job is not in processing state.");
        }

        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        return Result.Ok();
    }

    public static Result Fail(SummaryJob job, string errorMessage)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = errorMessage;
        job.CompletedAt = DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
