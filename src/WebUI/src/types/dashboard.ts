// Dashboard types
export interface DashboardStats {
  totalServers: number;
  runningServers: number;
  activeDomains: number;
  totalDomains: number;
  totalDatabases: number;
  activeDatabases: number;
  systemInfo: SystemInfo;
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
  name: string;
  cpuUsagePercent: number;
  memoryUsageMB: number;
  processId: number;
}