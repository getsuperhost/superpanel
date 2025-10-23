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
  dnsRecords?: DnsRecord[];
  dnsZone?: DnsZone;
  dnsPropagationStatus?: DnsPropagationStatus;
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

// DNS-related type definitions

export enum DnsRecordType {
  A = 'A',
  AAAA = 'AAAA',
  CNAME = 'CNAME',
  MX = 'MX',
  TXT = 'TXT',
  SRV = 'SRV',
  PTR = 'PTR',
  NS = 'NS',
  SOA = 'SOA'
}

export enum DnsRecordStatus {
  Active = 1,
  Inactive = 2,
  Pending = 3,
  Error = 4
}

export interface DnsRecord {
  id: number;
  domainId: number;
  domain?: Domain;
  name: string; // e.g., "www", "@", "mail"
  type: DnsRecordType;
  value: string; // e.g., IP address, target domain, text content
  ttl: number; // Time to live in seconds
  priority?: number; // For MX and SRV records
  weight?: number; // For SRV records
  port?: number; // For SRV records
  status: DnsRecordStatus;
  lastChecked?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface DnsZone {
  id: number;
  domainId: number;
  domain?: Domain;
  zoneFile: string; // Raw zone file content
  serial: number; // Zone serial number
  refresh: number; // Refresh interval
  retry: number; // Retry interval
  expire: number; // Expire interval
  minimum: number; // Minimum TTL
  nameservers: string[]; // List of nameservers
  lastModified: string;
  createdAt: string;
}

export interface DnsPropagationStatus {
  domainId: number;
  domainName: string;
  nameservers: string[];
  records: {
    type: DnsRecordType;
    name: string;
    expectedValue: string;
    actualValues: string[];
    isPropagated: boolean;
    lastChecked: string;
  }[];
  overallStatus: 'propagating' | 'propagated' | 'error';
  lastChecked: string;
}