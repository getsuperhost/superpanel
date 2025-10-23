// API Configuration
export const API_BASE_URL = process.env.NODE_ENV === "production"
  ? "" // In production, rely on relative URLs via nginx proxy
  : "https://localhost:5001"; // Use HTTPS in development

// Security validation function to ensure HTTPS (relaxed for Docker proxy)
const validateSecureUrl = (url: string): string => {
  // Allow empty base or relative paths in production (Docker/nginx environment)
  if (process.env.NODE_ENV === "production" && (url === "" || url.startsWith("/"))) {
    return url;
  }
  // Allow relative URLs in any environment
  if (url.startsWith("/")) {
    return url;
  }
  // Require HTTPS for absolute URLs
  if (url && !url.startsWith("https://")) {
    throw new Error("Insecure URL detected. Only HTTPS connections are allowed.");
  }
  return url;
};

// Import types
import { SslCertificate, CertificateRequest, CertificateInstallRequest } from "../types";
import {
  EmailAccount,
  EmailForwarder,
  EmailAlias,
  CreateEmailAccountRequest,
  UpdateEmailAccountRequest,
  CreateForwarderRequest,
  CreateAliasRequest
} from "../types/email";
import {
  Backup,
  BackupLog,
  BackupSchedule,
  BackupRequest,
  RestoreRequest,
  RestoreResult,
  BackupStats
} from "../types/backup";
import {
  AlertRule,
  Alert,
  AlertStats,
  CreateAlertRuleRequest,
  UpdateAlertRuleRequest,
  AlertRuleStatus,
  AlertHistory,
  AlertComment
} from "../types/alerts";
import { DashboardStats } from "../types/dashboard";
import { DnsRecord, DnsZone, DnsPropagationStatus, DnsRecordType, DnsRecordStatus } from "../types/domains";

// Types
export interface Server {
  id: number;
  name: string;
  ipAddress: string;
  port: number;
  status: ServerStatus;
  operatingSystem: string;
  totalMemoryMB: number;
  availableMemoryMB: number;
  cpuUsagePercent: number;
  diskUsagePercent: number;
  createdAt: string;
  lastUpdated: string;
}

export interface User {
  id: number;
  username: string;
  email: string;
  role: string;
}

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

export interface SystemInfo {
  serverName: string;
  operatingSystem: string;
  architecture: string;
  cpuUsagePercent: number;
  totalMemoryMB: number;
  availableMemoryMB: number;
  drives: DriveInfo[];
  topProcesses: ProcessInfo[];
  lastUpdated: string;
}

export interface DriveInfo {
  name: string;
  fileSystem: string;
  totalSizeGB: number;
  availableSpaceGB: number;
  usagePercent: number;
}

export interface ProcessInfo {
  id: number;
  name: string;
  memoryMB: number;
  cpuPercent: number;
}

export interface FileSystemItem {
  name: string;
  fullPath: string;
  isDirectory: boolean;
  sizeBytes: number;
  lastModified: string;
  permissions: string;
}

export interface Database {
  id: number;
  name: string;
  type: string;
  username?: string;
  sizeInMB: number;
  serverId: number;
  serverName?: string;
  status: DatabaseStatus;
  createdAt: string;
  backupDate?: string;
}

export interface HealthInfo {
  status: string;
  timestamp: string;
  version?: string;
}

export interface SystemHealthInfo {
  machineName: string;
  osVersion: string;
  processorCount: number;
  workingSet: number;
  timestamp: string;
}

export enum DatabaseStatus {
  Active = "Active",
  Inactive = "Inactive",
  Suspended = "Suspended",
  Corrupted = "Corrupted"
}

export enum ServerStatus {
  Running = "Running",
  Stopped = "Stopped",
  Error = "Error",
  Maintenance = "Maintenance"
}

// API Client Class
class ApiClient {
  private baseUrl: string;
  private getToken: () => string | null;

  constructor(baseUrl: string, getToken: () => string | null) {
    this.baseUrl = validateSecureUrl(baseUrl);
    this.getToken = getToken;
  }

  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      "Content-Type": "application/json",
      "X-Requested-With": "XMLHttpRequest",
      "Cache-Control": "no-cache",
      "Pragma": "no-cache",
    };

    const token = this.getToken();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    return headers;
  }

  private async handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
      const errorMessage = await response.text();
      throw new Error(`API Error: ${response.status} - ${errorMessage}`);
    }

    // Handle empty responses (like 204 No Content)
    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
  }

  async get<T>(endpoint: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: "GET",
      headers: this.getHeaders(),
      credentials: "same-origin",
      mode: "cors",
      cache: "no-cache",
    });

    return this.handleResponse<T>(response);
  }

  async post<T>(endpoint: string, data?: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: "POST",
      headers: this.getHeaders(),
      body: data ? JSON.stringify(data) : undefined,
      credentials: "same-origin",
      mode: "cors",
      cache: "no-cache",
    });

    return this.handleResponse<T>(response);
  }

  async put<T>(endpoint: string, data: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: "PUT",
      headers: this.getHeaders(),
      body: JSON.stringify(data),
      credentials: "same-origin",
      mode: "cors",
      cache: "no-cache",
    });

    return this.handleResponse<T>(response);
  }

  async delete(endpoint: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: "DELETE",
      headers: this.getHeaders(),
      credentials: "same-origin",
      mode: "cors",
      cache: "no-cache",
    });

    await this.handleResponse<void>(response);
  }

  async patch<T>(endpoint: string, data: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${endpoint}`, {
      method: "PATCH",
      headers: this.getHeaders(),
      body: JSON.stringify(data),
      credentials: "same-origin",
      mode: "cors",
      cache: "no-cache",
    });

    return this.handleResponse<T>(response);
  }
}

// Create API client instance with token getter
const getAuthToken = () => localStorage.getItem("authToken");
const apiClient = new ApiClient(API_BASE_URL, getAuthToken);

// Server API functions
export const serverApi = {
  getAll: (): Promise<Server[]> => apiClient.get("/api/servers"),
  getById: (id: number): Promise<Server> => apiClient.get(`/api/servers/${id}`),
  create: (server: Omit<Server, "id" | "createdAt" | "lastUpdated">): Promise<Server> =>
    apiClient.post("/api/servers", server),
  update: (id: number, server: Partial<Server>): Promise<Server> =>
    apiClient.put(`/api/servers/${id}`, server),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/servers/${id}`),
  updateStatus: (id: number, status: ServerStatus): Promise<void> =>
    apiClient.patch(`/api/servers/${id}/status`, status),
  getSystemInfo: (): Promise<SystemInfo> => apiClient.get("/api/servers/system-info"),
};

// Domain API functions
export const domainApi = {
  getAll: (): Promise<Domain[]> => apiClient.get("/api/domains"),
  getById: (id: number): Promise<Domain> => apiClient.get(`/api/domains/${id}`),
  getByServerId: (serverId: number): Promise<Domain[]> =>
    apiClient.get(`/api/domains/server/${serverId}`),
  create: (domain: Omit<Domain, "id" | "createdAt" | "updatedAt" | "server" | "subdomains" | "dnsRecords" | "dnsZone" | "dnsPropagationStatus">): Promise<Domain> =>
    apiClient.post("/api/domains", domain),
  update: (id: number, domain: Partial<Domain>): Promise<Domain> =>
    apiClient.put(`/api/domains/${id}`, domain),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/domains/${id}`),

  // DNS Record Management
  getDnsRecords: (domainId: number): Promise<DnsRecord[]> =>
    apiClient.get(`/api/domains/${domainId}/dns-records`),
  createDnsRecord: (domainId: number, record: Omit<DnsRecord, "id" | "domainId" | "createdAt" | "updatedAt">): Promise<DnsRecord> =>
    apiClient.post(`/api/domains/${domainId}/dns-records`, record),
  updateDnsRecord: (domainId: number, recordId: number, record: Partial<DnsRecord>): Promise<DnsRecord> =>
    apiClient.put(`/api/domains/${domainId}/dns-records/${recordId}`, record),
  deleteDnsRecord: (domainId: number, recordId: number): Promise<void> =>
    apiClient.delete(`/api/domains/${domainId}/dns-records/${recordId}`),

  // DNS Zone Management
  getDnsZone: (domainId: number): Promise<DnsZone> =>
    apiClient.get(`/api/domains/${domainId}/dns-zone`),
  updateDnsZone: (domainId: number, zone: Partial<DnsZone>): Promise<DnsZone> =>
    apiClient.put(`/api/domains/${domainId}/dns-zone`, zone),

  // DNS Propagation Monitoring
  getDnsPropagationStatus: (domainId: number): Promise<DnsPropagationStatus> =>
    apiClient.get(`/api/domains/${domainId}/dns-propagation`),
  checkDnsPropagation: (domainId: number): Promise<DnsPropagationStatus> =>
    apiClient.post(`/api/domains/${domainId}/dns-propagation/check`),
};

// File API functions
export const fileApi = {
  browse: (path: string = "/"): Promise<FileSystemItem[]> =>
    apiClient.get(`/api/files/browse?path=${encodeURIComponent(path)}`),
  getInfo: (path: string): Promise<FileSystemItem> =>
    apiClient.get(`/api/files/info?path=${encodeURIComponent(path)}`),
  readFile: (filePath: string): Promise<{ content: string }> =>
    apiClient.get(`/api/files/content?filePath=${encodeURIComponent(filePath)}`),
  writeFile: (filePath: string, content: string): Promise<void> =>
    apiClient.post("/api/files/content", { filePath, content }),
  deleteFile: (filePath: string): Promise<void> =>
    apiClient.delete(`/api/files?filePath=${encodeURIComponent(filePath)}`),
  createDirectory: (path: string): Promise<void> =>
    apiClient.post("/api/files/directory", { path }),
  deleteDirectory: (path: string): Promise<void> =>
    apiClient.delete(`/api/files/directory?path=${encodeURIComponent(path)}`),
  move: (sourcePath: string, destinationPath: string): Promise<void> =>
    apiClient.post("/api/files/move", { sourcePath, destinationPath }),
  copy: (sourcePath: string, destinationPath: string): Promise<void> =>
    apiClient.post("/api/files/copy", { sourcePath, destinationPath }),
};

// Database API functions
export const databaseApi = {
  getAll: (): Promise<Database[]> => apiClient.get("/api/databases"),
  getById: (id: number): Promise<Database> => apiClient.get(`/api/databases/${id}`),
  getByServerId: (serverId: number): Promise<Database[]> =>
    apiClient.get(`/api/databases/server/${serverId}`),
  create: (database: Omit<Database, "id" | "createdAt" | "backupDate">): Promise<Database> =>
    apiClient.post("/api/databases", database),
  update: (id: number, database: Partial<Database>): Promise<Database> =>
    apiClient.put(`/api/databases/${id}`, database),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/databases/${id}`),
};

// Health API for testing
export const healthApi = {
  getHealth: (): Promise<HealthInfo> => apiClient.get("/api/health"),
  getSystem: (): Promise<SystemHealthInfo> => apiClient.get("/api/health/system"),
  getMockServers: (): Promise<Server[]> => apiClient.get("/api/health/mock-servers"),
  getMockDomains: (): Promise<Domain[]> => apiClient.get("/api/health/mock-domains"),
};

// Dashboard stats API
export const dashboardApi = {
  getStats: async (): Promise<DashboardStats> => {
    try {
      const response = await apiClient.get<DashboardStats>("/api/dashboard/stats");
      return response;
    } catch (error) {
      console.error("Failed to fetch dashboard stats:", error);
      throw error;
    }
  },
};

// User API functions
export const userApi = {
  getAll: (): Promise<User[]> => apiClient.get("/api/users"),
  getById: (id: number): Promise<User> => apiClient.get(`/api/users/${id}`),
  create: (user: Omit<User, "id"> & { password: string }): Promise<User> =>
    apiClient.post("/api/users", user),
  update: (id: number, user: Partial<User & { password?: string }>): Promise<User> =>
    apiClient.put(`/api/users/${id}`, user),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/users/${id}`),
};

// SSL Certificate API functions
export const sslCertificateApi = {
  getAll: (): Promise<SslCertificate[]> => apiClient.get("/api/ssl-certificates"),
  getById: (id: number): Promise<SslCertificate> => apiClient.get(`/api/ssl-certificates/${id}`),
  getByDomain: (domainId: number): Promise<SslCertificate[]> =>
    apiClient.get(`/api/ssl-certificates/domain/${domainId}`),
  getExpiringSoon: (days: number = 30): Promise<SslCertificate[]> =>
    apiClient.get(`/api/ssl-certificates/expiring-soon?days=${days}`),
  request: (request: CertificateRequest): Promise<SslCertificate> =>
    apiClient.post("/api/ssl-certificates/request", request),
  install: (id: number, installRequest: CertificateInstallRequest): Promise<void> =>
    apiClient.put(`/api/ssl-certificates/${id}/install`, installRequest),
  renew: (id: number): Promise<void> => apiClient.put(`/api/ssl-certificates/${id}/renew`, {}),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/ssl-certificates/${id}`),
};

// Email API functions
export const emailApi = {
  getAll: (): Promise<EmailAccount[]> => apiClient.get("/api/emails"),
  getById: (id: number): Promise<EmailAccount> => apiClient.get(`/api/emails/${id}`),
  getByDomain: (domainId: number): Promise<EmailAccount[]> =>
    apiClient.get(`/api/emails/domain/${domainId}`),
  create: (request: CreateEmailAccountRequest): Promise<EmailAccount> =>
    apiClient.post("/api/emails", request),
  update: (id: number, request: UpdateEmailAccountRequest): Promise<void> =>
    apiClient.put(`/api/emails/${id}`, request),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/emails/${id}`),
  addForwarder: (emailAccountId: number, request: CreateForwarderRequest): Promise<EmailForwarder> =>
    apiClient.post(`/api/emails/${emailAccountId}/forwarders`, request),
  deleteForwarder: (forwarderId: number): Promise<void> =>
    apiClient.delete(`/api/emails/forwarders/${forwarderId}`),
  addAlias: (emailAccountId: number, request: CreateAliasRequest): Promise<EmailAlias> =>
    apiClient.post(`/api/emails/${emailAccountId}/aliases`, request),
  deleteAlias: (aliasId: number): Promise<void> =>
    apiClient.delete(`/api/emails/aliases/${aliasId}`),
};

// Backup API functions
export const backupApi = {
  getAll: (): Promise<Backup[]> => apiClient.get("/api/backups"),
  getById: (id: number): Promise<Backup> => apiClient.get(`/api/backups/${id}`),
  getByServerId: (serverId: number): Promise<Backup[]> =>
    apiClient.get(`/api/backups/server/${serverId}`),
  getByDatabaseId: (databaseId: number): Promise<Backup[]> =>
    apiClient.get(`/api/backups/database/${databaseId}`),
  getByDomainId: (domainId: number): Promise<Backup[]> =>
    apiClient.get(`/api/backups/domain/${domainId}`),
  create: (request: BackupRequest): Promise<Backup> =>
    apiClient.post("/api/backups", request),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/backups/${id}`),
  restore: (id: number, request: RestoreRequest): Promise<RestoreResult> =>
    apiClient.post(`/api/backups/${id}/restore`, request),
  download: async (id: number): Promise<Blob> => {
    const response = await fetch(`${API_BASE_URL}/api/backups/${id}/download`, {
      method: "GET",
      headers: {
        "Authorization": `Bearer ${getAuthToken()}`,
      },
      credentials: "same-origin",
      mode: "cors",
    });

    if (!response.ok) {
      throw new Error(`Download failed: ${response.status}`);
    }

    return response.blob();
  },
  getLogs: (id: number): Promise<BackupLog[]> =>
    apiClient.get(`/api/backups/${id}/logs`),
  getStats: (): Promise<BackupStats> => apiClient.get("/api/backups/stats"),
  cancel: (id: number): Promise<void> =>
    apiClient.post(`/api/backups/${id}/cancel`, {}),
};

// Backup Schedule API functions
export const backupScheduleApi = {
  getAll: (): Promise<BackupSchedule[]> => apiClient.get("/api/backup-schedules"),
  getById: (id: number): Promise<BackupSchedule> => apiClient.get(`/api/backup-schedules/${id}`),
  create: (schedule: Omit<BackupSchedule, "id" | "createdAt" | "lastRunAt" | "nextRunAt">): Promise<BackupSchedule> =>
    apiClient.post("/api/backup-schedules", schedule),
  update: (id: number, schedule: Partial<BackupSchedule>): Promise<BackupSchedule> =>
    apiClient.put(`/api/backup-schedules/${id}`, schedule),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/backup-schedules/${id}`),
  enable: (id: number): Promise<void> =>
    apiClient.patch(`/api/backup-schedules/${id}/enable`, {}),
  disable: (id: number): Promise<void> =>
    apiClient.patch(`/api/backup-schedules/${id}/disable`, {}),
};

// Alert Rules API functions
export const alertRulesApi = {
  getAll: (): Promise<AlertRule[]> => apiClient.get("/api/alertrules"),
  getById: (id: number): Promise<AlertRule> => apiClient.get(`/api/alertrules/${id}`),
  create: (rule: CreateAlertRuleRequest): Promise<AlertRule> =>
    apiClient.post("/api/alertrules", rule),
  update: (id: number, rule: UpdateAlertRuleRequest): Promise<AlertRule> =>
    apiClient.put(`/api/alertrules/${id}`, rule),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/alertrules/${id}`),
  test: (id: number): Promise<{ message: string; alertId: number }> =>
    apiClient.post(`/api/alertrules/${id}/test`, {}),
};

// Alerts API functions
export const alertsApi = {
  getAll: (serverId?: number, status?: AlertRuleStatus): Promise<Alert[]> => {
    const params = new URLSearchParams();
    if (serverId) params.append('serverId', serverId.toString());
    if (status) params.append('status', status);
    const query = params.toString();
    return apiClient.get(`/api/alerts${query ? `?${query}` : ''}`);
  },
  getById: (id: number): Promise<Alert> => apiClient.get(`/api/alerts/${id}`),
  create: (alert: Omit<Alert, 'id' | 'createdAt' | 'updatedAt'>): Promise<Alert> =>
    apiClient.post("/api/alerts", alert),
  acknowledge: (id: number): Promise<Alert> =>
    apiClient.put(`/api/alerts/${id}/acknowledge`, {}),
  resolve: (id: number): Promise<Alert> =>
    apiClient.put(`/api/alerts/${id}/resolve`, {}),
  acknowledgeWithComment: (id: number, comment?: string, user?: string): Promise<{ message: string }> =>
    apiClient.put(`/api/alerts/${id}/acknowledge-with-comment`, { comment, user }),
  resolveWithComment: (id: number, comment?: string, user?: string): Promise<{ message: string }> =>
    apiClient.put(`/api/alerts/${id}/resolve-with-comment`, { comment, user }),
  getHistory: (id: number): Promise<AlertHistory[]> =>
    apiClient.get(`/api/alerts/${id}/history`),
  getComments: (id: number): Promise<AlertComment[]> =>
    apiClient.get(`/api/alerts/${id}/comments`),
  addComment: (id: number, comment: string, commentType?: string, user?: string): Promise<AlertComment> =>
    apiClient.post(`/api/alerts/${id}/comments`, { comment, commentType, user }),
  getStats: (): Promise<AlertStats> => apiClient.get("/api/alerts/stats"),
  evaluate: (): Promise<{ message: string }> =>
    apiClient.post("/api/alerts/evaluate", {}),
  delete: (id: number): Promise<void> => apiClient.delete(`/api/alerts/${id}`),
};

export default apiClient;
