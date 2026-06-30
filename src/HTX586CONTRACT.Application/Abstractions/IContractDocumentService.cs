namespace HTX586CONTRACT.Application.Abstractions;
public interface IContractDocumentService
{
    Task<string> GeneratePdfAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<string> SaveSignatureAsync(Guid contractId, string party, string signerName, string dataUrl, CancellationToken cancellationToken = default);
}
