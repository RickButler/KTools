namespace SharedKernel;

public interface IServiceStatus
{
    ServiceStatus Status { get; }
    event EventHandler? StatusChanged;
}