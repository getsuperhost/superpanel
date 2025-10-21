// SSL Certificate Types
export interface SslCertificate {
  id: number;
  domainId: number;
  domain?: {
    id: number;
    name: string;
  };
  certificatePath: string;
  privateKeyPath: string;
  certificateAuthorityPath?: string;
  expiryDate: string;
  issuedDate: string;
  issuer: string;
  subject: string;
  serialNumber: string;
  status: CertificateStatus;
  type: CertificateType;
  autoRenew: boolean;
  createdAt: string;
  updatedAt: string;
}

export enum CertificateStatus {
  Pending = 'Pending',
  Active = 'Active',
  Expired = 'Expired',
  Revoked = 'Revoked',
  Failed = 'Failed'
}

export enum CertificateType {
  DV = 'DV', // Domain Validated
  OV = 'OV', // Organization Validated
  EV = 'EV', // Extended Validation
  SelfSigned = 'SelfSigned'
}

export interface CertificateRequest {
  domainId: number;
  type: CertificateType;
  autoRenew: boolean;
  email?: string; // Contact email for certificate notifications
}

export interface CertificateInstallRequest {
  certificateContent: string;
  privateKeyContent: string;
  certificateAuthorityContent?: string;
}

export interface CertificateStats {
  total: number;
  active: number;
  expiringSoon: number;
  expired: number;
}