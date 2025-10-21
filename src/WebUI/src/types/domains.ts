// Domain-related type definitions

export interface Domain {
  id: number;
  name: string;
  documentRoot?: string;
  status: DomainStatus;
  sslEnabled: boolean;
  sslExpiry?: string;
  userId: number;
  serverId: number;
  server?: Server;
  createdAt: string;
  updatedAt?: string;
  subdomains?: Subdomain[];
}

export interface Subdomain {
  id: number;
  name: string;
  domainId: number;
  domain?: Domain;
  documentRoot?: string;
  status: SubdomainStatus;
  createdAt: string;
}

export enum DomainStatus {
  Active = 1,
  Inactive = 2,
  Suspended = 3,
  Expired = 4
}

export enum SubdomainStatus {
  Active = 1,
  Inactive = 2,
  Suspended = 3
}

// Import Server from existing types
interface Server {
  id: number;
  name: string;
  ipAddress: string;
  port: number;
  status: string;
  operatingSystem: string;
  totalMemoryMB: number;
  availableMemoryMB: number;
  cpuUsagePercent: number;
  diskUsagePercent: number;
  createdAt: string;
  lastUpdated: string;
}