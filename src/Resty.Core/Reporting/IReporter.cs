using Resty.Core.Models;

namespace Resty.Core.Reporting;

public interface IReporter
{
    Task WriteAsync(IReadOnlyList<RequestExecutionResult> results, TextWriter output);
}
