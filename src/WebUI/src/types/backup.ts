// Backup types and interfaces
export enum BackupType {
  Database = "Database",
  Files = "Files",
  FullServer = "FullServer",
  Website = "Website",
  Email = "Email"
}

export enum BackupStatus {
  Pending = "Pending",
  InProgress = "InProgress",
  Completed = "Completed",
  Failed = "Failed",
  Cancelled = "Cancelled"
}

export interface Backup {
  id: number;
  name: string;
  description?: string;
  type: BackupType;
  status: BackupStatus;
  serverId?: number;
  serverName?: string;
  databaseId?: number;
  databaseName?: string;
  domainId?: number;
  domainName?: string;
  backupPath?: string;
  filePath?: string;
  fileSizeInBytes?: number;
  isCompressed: boolean;
  isEncrypted: boolean;
  retentionDays: number;
  createdByUserId: number;
  createdByUsername?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}

export interface BackupLog {
  id: number;
  backupId: number;
  level: string;
  message: string;
  details?: string;
  timestamp: string;
}

export interface BackupSchedule {
  id: number;
  name: string;
  description?: string;
  backupType: BackupType;
  serverId?: number;
  databaseId?: number;
  domainId?: number;
  backupPath?: string;
  isCompressed: boolean;
  isEncrypted: boolean;
  retentionDays: number;
  scheduleExpression: string; // Cron expression
  isActive: boolean;
  lastRunAt?: string;
  nextRunAt?: string;
  createdByUserId: number;
  createdAt: string;
}

export interface BackupRequest {
  name: string;
  description?: string;
  type: BackupType;
  serverId?: number;
  databaseId?: number;
  domainId?: number;
  backupPath?: string;
  isCompressed: boolean;
  isEncrypted: boolean;
  retentionDays: number;
}

export interface RestoreRequest {
  restorePath?: string;
  overwriteExisting: boolean;
}

export interface RestoreResult {
  success: boolean;
  message: string;
  bytesRestored: number;
}

export interface BackupStats {
  totalBackups: number;
  successfulBackups: number;
  failedBackups: number;
  totalSizeGB: number;
  averageBackupTime: number;
  lastBackupDate?: string;
}