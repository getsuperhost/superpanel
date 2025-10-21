export interface ServerMetrics {
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  networkIn: number;
  networkOut: number;
  activeConnections: number;
  timestamp: string;
  status: ServerStatus;
}

export enum ServerStatus {
  Online = 1,
  Offline = 2,
  Warning = 3,
  Critical = 4
}

export interface ServerAlert {
  serverId: number;
  serverName: string;
  type: AlertType;
  message: string;
  severity: AlertSeverity;
  timestamp: string;
}

export enum AlertType {
  CpuHigh = 'CpuHigh',
  MemoryHigh = 'MemoryHigh',
  DiskFull = 'DiskFull',
  ServerDown = 'ServerDown',
  NetworkIssue = 'NetworkIssue'
}

export enum AlertSeverity {
  Info = 'Info',
  Warning = 'Warning',
  Critical = 'Critical'
}

export interface MonitoringState {
  metrics: Record<number, ServerMetrics>;
  alerts: ServerAlert[];
  isConnected: boolean;
  subscribedServers: number[];
}