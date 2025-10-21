namespace SuperPanel.WebAPI.Services;

public interface ISslCertificateService
{
    Task<bool> RequestCertificateAsync(int certificateId);
    Task<bool> RenewCertificateAsync(int certificateId);
    Task<bool> ValidateCertificateAsync(int certificateId);
}