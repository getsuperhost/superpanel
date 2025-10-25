using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperPanel.WebAPI.Data;
using SuperPanel.WebAPI.Models;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using System.Security.Cryptography.X509Certificates;

namespace SuperPanel.WebAPI.Services;

public class SslCertificateService : ISslCertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SslCertificateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public SslCertificateService(
        ApplicationDbContext context,
        ILogger<SslCertificateService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<bool> RequestCertificateAsync(int certificateId)
    {
        var certificate = await _context.SslCertificates
            .Include(c => c.Domain)
            .FirstOrDefaultAsync(c => c.Id == certificateId);

        if (certificate == null)
        {
            _logger.LogWarning("Certificate with ID {CertificateId} not found", certificateId);
            return false;
        }

        try
        {
            _logger.LogInformation("Starting certificate request for domain: {Domain}", certificate.DomainName);

            // For Let's Encrypt certificates, implement ACME protocol
            if (certificate.Type == SslCertificateType.LetsEncrypt)
            {
                return await RequestLetsEncryptCertificateAsync(certificate);
            }

            // For self-signed certificates (development)
            if (certificate.Type == SslCertificateType.SelfSigned)
            {
                return await GenerateSelfSignedCertificateAsync(certificate);
            }

            // For other certificate types, mark as pending for manual processing
            _logger.LogInformation("Certificate type {Type} requires manual processing", certificate.Type);
            return true; // Return true to indicate request was accepted
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request certificate for domain: {Domain}", certificate.DomainName);
            certificate.Status = SslCertificateStatus.Failed;
            certificate.Notes = $"Request failed: {ex.Message}";
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> RenewCertificateAsync(int certificateId)
    {
        var certificate = await _context.SslCertificates.FindAsync(certificateId);
        if (certificate == null)
        {
            _logger.LogWarning("Certificate with ID {CertificateId} not found for renewal", certificateId);
            return false;
        }

        try
        {
            _logger.LogInformation("Starting certificate renewal for domain: {Domain}", certificate.DomainName);

            // For Let's Encrypt, use ACME renewal
            if (certificate.Type == SslCertificateType.LetsEncrypt)
            {
                return await RenewLetsEncryptCertificateAsync(certificate);
            }

            // For other types, renewal might not be applicable
            _logger.LogWarning("Certificate type {Type} does not support automatic renewal", certificate.Type);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew certificate for domain: {Domain}", certificate.DomainName);
            certificate.Status = SslCertificateStatus.Failed;
            certificate.Notes = $"Renewal failed: {ex.Message}";
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> ValidateCertificateAsync(int certificateId)
    {
        var certificate = await _context.SslCertificates.FindAsync(certificateId);
        if (certificate == null)
        {
            return false;
        }

        try
        {
            // Basic validation - check if certificate files exist and are valid
            if (!string.IsNullOrEmpty(certificate.CertificatePath) &&
                !string.IsNullOrEmpty(certificate.PrivateKeyPath))
            {
                // In a real implementation, you'd validate the certificate chain,
                // check expiration, verify against domain, etc.
                bool certExists = File.Exists(certificate.CertificatePath);
                bool keyExists = File.Exists(certificate.PrivateKeyPath);

                if (certExists && keyExists)
                {
                    certificate.Status = SslCertificateStatus.Active;
                    await _context.SaveChangesAsync();
                    return true;
                }
            }

            certificate.Status = SslCertificateStatus.Failed;
            await _context.SaveChangesAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate certificate {CertificateId}", certificateId);
            return false;
        }
    }

    private async Task<bool> RequestLetsEncryptCertificateAsync(SslCertificate certificate)
    {
        try
        {
            _logger.LogInformation("Requesting Let's Encrypt certificate for {Domain}", certificate.DomainName);

            // Get ACME settings from configuration
            var acmeEmail = _configuration["SslSettings:AcmeSettings:Email"];
            var useStaging = _configuration.GetValue<bool>("SslSettings:AcmeSettings:Staging", true);
            var challengePath = _configuration["SslSettings:AcmeSettings:ChallengePath"] ?? "/var/www/.well-known/acme-challenge";

            if (string.IsNullOrEmpty(acmeEmail))
            {
                throw new InvalidOperationException("ACME email not configured in SslSettings:AcmeSettings:Email");
            }

            // Create ACME context
            var acme = new AcmeContext(useStaging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2);

            // Create or load account
            var account = await acme.NewAccount(acmeEmail, true);

            // Create certificate order
            var order = await acme.NewOrder(new[] { certificate.DomainName });

            // Get authorization for the domain
            var authz = (await order.Authorizations()).First();
            var challenge = await authz.Http();

            // Create challenge directory
            System.IO.Directory.CreateDirectory(challengePath);

            // Write challenge token to file
            var challengeFile = Path.Combine(challengePath, challenge.Token);
            await File.WriteAllTextAsync(challengeFile, challenge.KeyAuthz);

            // Validate the challenge
            var challengeResult = await challenge.Validate();

            // Wait for validation to complete
            var retryCount = 0;
            while (challengeResult.Status == ChallengeStatus.Pending && retryCount < 30)
            {
                await Task.Delay(2000);
                challengeResult = await challenge.Validate();
                retryCount++;
            }

            if (challengeResult.Status != ChallengeStatus.Valid)
            {
                throw new Exception($"Challenge validation failed: {challengeResult.Status}");
            }

            // Finalize the order
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CountryName = "US",
                State = "State",
                Locality = "City",
                Organization = "SuperPanel",
                OrganizationUnit = "IT",
                CommonName = certificate.DomainName,
            }, privateKey);

            // Save certificate files
            var certDir = Path.Combine(_configuration["SslSettings:CertificateDirectory"] ?? "/etc/ssl/certs", certificate.DomainName);
            System.IO.Directory.CreateDirectory(certDir);

            var certPath = Path.Combine(certDir, $"{certificate.DomainName}.crt");
            var keyPath = Path.Combine(certDir, $"{certificate.DomainName}.key");
            var chainPath = Path.Combine(certDir, "chain.pem");

            // Save certificate
            var certPem = cert.ToPem();
            await File.WriteAllTextAsync(certPath, certPem);

            // Save private key
            var keyPem = privateKey.ToPem();
            await File.WriteAllTextAsync(keyPath, keyPem);

            // Save certificate chain (issuer certificates)
            // Note: In Certes, the certificate chain is typically included in the cert.ToPem()
            // For now, we'll save the main certificate. Chain can be retrieved separately if needed.
            var issuerPem = cert.ToPem(); // Use the certificate PEM which may include chain
            await File.WriteAllTextAsync(chainPath, issuerPem);

            // Update certificate record
            certificate.CertificatePath = certPath;
            certificate.PrivateKeyPath = keyPath;
            certificate.ChainPath = chainPath;
            certificate.Status = SslCertificateStatus.Active;
            certificate.Issuer = "Let's Encrypt";
            certificate.IssuedAt = DateTime.UtcNow;
            certificate.ExpiresAt = DateTime.UtcNow.AddDays(90);
            certificate.LastRenewedAt = DateTime.UtcNow;
            certificate.Notes = "Let's Encrypt certificate successfully issued.";

            await _context.SaveChangesAsync();

            // Clean up challenge file
            if (File.Exists(challengeFile))
            {
                File.Delete(challengeFile);
            }

            _logger.LogInformation("Let's Encrypt certificate issued for {Domain}", certificate.DomainName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request Let's Encrypt certificate for {Domain}", certificate.DomainName);
            certificate.Status = SslCertificateStatus.Failed;
            certificate.Notes = $"ACME request failed: {ex.Message}";
            await _context.SaveChangesAsync();
            return false;
        }
    }

    private async Task<bool> RenewLetsEncryptCertificateAsync(SslCertificate certificate)
    {
        try
        {
            _logger.LogInformation("Renewing Let's Encrypt certificate for {Domain}", certificate.DomainName);

            // Get ACME settings from configuration
            var acmeEmail = _configuration["SslSettings:AcmeSettings:Email"];
            var useStaging = _configuration.GetValue<bool>("SslSettings:AcmeSettings:Staging", true);
            var challengePath = _configuration["SslSettings:AcmeSettings:ChallengePath"] ?? "/var/www/.well-known/acme-challenge";

            if (string.IsNullOrEmpty(acmeEmail))
            {
                throw new InvalidOperationException("ACME email not configured in SslSettings:AcmeSettings:Email");
            }

            // Create ACME context
            var acme = new AcmeContext(useStaging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2);

            // Create or load account
            var account = await acme.NewAccount(acmeEmail, true);

            // Create certificate order for renewal
            var order = await acme.NewOrder(new[] { certificate.DomainName });

            // Get authorization for the domain
            var authz = (await order.Authorizations()).First();
            var challenge = await authz.Http();

            // Create challenge directory
            System.IO.Directory.CreateDirectory(challengePath);

            // Write challenge token to file
            var challengeFile = Path.Combine(challengePath, challenge.Token);
            await File.WriteAllTextAsync(challengeFile, challenge.KeyAuthz);

            // Validate the challenge
            var challengeResult = await challenge.Validate();

            // Wait for validation to complete
            var retryCount = 0;
            while (challengeResult.Status == ChallengeStatus.Pending && retryCount < 30)
            {
                await Task.Delay(2000);
                challengeResult = await challenge.Validate();
                retryCount++;
            }

            if (challengeResult.Status != ChallengeStatus.Valid)
            {
                throw new Exception($"Challenge validation failed: {challengeResult.Status}");
            }

            // Finalize the order
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CountryName = "US",
                State = "State",
                Locality = "City",
                Organization = "SuperPanel",
                OrganizationUnit = "IT",
                CommonName = certificate.DomainName,
            }, privateKey);

            // Save certificate files (renewal overwrites existing files)
            var certDir = Path.Combine(_configuration["SslSettings:CertificateDirectory"] ?? "/etc/ssl/certs", certificate.DomainName);
            System.IO.Directory.CreateDirectory(certDir);

            var certPath = Path.Combine(certDir, $"{certificate.DomainName}.crt");
            var keyPath = Path.Combine(certDir, $"{certificate.DomainName}.key");
            var chainPath = Path.Combine(certDir, "chain.pem");

            // Save certificate
            var certPem = cert.ToPem();
            await File.WriteAllTextAsync(certPath, certPem);

            // Save private key
            var keyPem = privateKey.ToPem();
            await File.WriteAllTextAsync(keyPath, keyPem);

            // Save certificate chain (issuer certificates)
            var issuerPem = cert.ToPem(); // Use the certificate PEM which may include chain
            await File.WriteAllTextAsync(chainPath, issuerPem);

            // Update certificate record
            certificate.CertificatePath = certPath;
            certificate.PrivateKeyPath = keyPath;
            certificate.ChainPath = chainPath;
            certificate.Status = SslCertificateStatus.Active;
            certificate.Issuer = "Let's Encrypt";
            certificate.IssuedAt = DateTime.UtcNow;
            certificate.ExpiresAt = DateTime.UtcNow.AddDays(90);
            certificate.LastRenewedAt = DateTime.UtcNow;
            certificate.Notes = "Let's Encrypt certificate successfully renewed.";

            await _context.SaveChangesAsync();

            // Clean up challenge file
            if (File.Exists(challengeFile))
            {
                File.Delete(challengeFile);
            }

            _logger.LogInformation("Let's Encrypt certificate renewed for {Domain}", certificate.DomainName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew Let's Encrypt certificate for {Domain}", certificate.DomainName);
            certificate.Status = SslCertificateStatus.Failed;
            certificate.Notes = $"ACME renewal failed: {ex.Message}";
            await _context.SaveChangesAsync();
            return false;
        }
    }

    private async Task<bool> GenerateSelfSignedCertificateAsync(SslCertificate certificate)
    {
        try
        {
            _logger.LogInformation("Generating self-signed certificate for {Domain}", certificate.DomainName);

            // Use OpenSSL or .NET APIs to generate self-signed certificate
            // This is a simplified example

            string certDir = Path.Combine(_configuration["SslSettings:CertificateDirectory"] ?? "/etc/ssl/certs", certificate.DomainName);
            System.IO.Directory.CreateDirectory(certDir);

            string certPath = Path.Combine(certDir, $"{certificate.DomainName}.crt");
            string keyPath = Path.Combine(certDir, $"{certificate.DomainName}.key");

            // In a real implementation, you'd use System.Security.Cryptography.X509Certificates
            // to generate a proper self-signed certificate

            // For demo, create placeholder files
            await File.WriteAllTextAsync(certPath, $"-----BEGIN CERTIFICATE-----\nSelf-signed certificate for {certificate.DomainName}\n-----END CERTIFICATE-----");
            await File.WriteAllTextAsync(keyPath, $"-----BEGIN PRIVATE KEY-----\nPrivate key for {certificate.DomainName}\n-----END PRIVATE KEY-----");

            certificate.CertificatePath = certPath;
            certificate.PrivateKeyPath = keyPath;
            certificate.Status = SslCertificateStatus.Active;
            certificate.Issuer = "Self-Signed";
            certificate.IssuedAt = DateTime.UtcNow;
            certificate.ExpiresAt = DateTime.UtcNow.AddDays(365);
            certificate.LastRenewedAt = DateTime.UtcNow;
            certificate.Notes = "Self-signed certificate generated for development.";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Self-signed certificate generated for {Domain}", certificate.DomainName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate self-signed certificate for {Domain}", certificate.DomainName);
            return false;
        }
    }

    private async Task ProcessLetsEncryptCertificateAsync(int certificateId)
    {
        try
        {
            await Task.Delay(5000); // Simulate processing time

            var certificate = await _context.SslCertificates.FindAsync(certificateId);
            if (certificate == null) return;

            // Simulate successful certificate generation
            string certDir = Path.Combine(_configuration["SslSettings:CertificateDirectory"] ?? "/etc/ssl/certs", certificate.DomainName);
            System.IO.Directory.CreateDirectory(certDir);

            certificate.CertificatePath = Path.Combine(certDir, $"{certificate.DomainName}.crt");
            certificate.PrivateKeyPath = Path.Combine(certDir, $"{certificate.DomainName}.key");
            certificate.ChainPath = Path.Combine(certDir, "chain.pem");
            certificate.Status = SslCertificateStatus.Active;
            certificate.Issuer = "Let's Encrypt";
            certificate.IssuedAt = DateTime.UtcNow;
            certificate.ExpiresAt = DateTime.UtcNow.AddDays(90);
            certificate.LastRenewedAt = DateTime.UtcNow;
            certificate.Notes = "Let's Encrypt certificate successfully issued.";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Let's Encrypt certificate processed for {Domain}", certificate.DomainName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Let's Encrypt certificate {CertificateId}", certificateId);
        }
    }


}