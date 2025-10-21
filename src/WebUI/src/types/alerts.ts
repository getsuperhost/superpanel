// Alert management types and interfaces
export enum AlertRuleSeverity {
  Info = "Info",
  Warning = "Warning",
  Critical = "Critical"
}

export enum AlertRuleStatus {
  Active = "Active",
  Acknowledged = "Acknowledged",
  Resolved = "Resolved"
}

export enum AlertRuleType {
  CpuUsage = "CpuUsage",
  MemoryUsage = "MemoryUsage",
  DiskUsage = "DiskUsage",
  ServiceDown = "ServiceDown",
  NetworkLatency = "NetworkLatency",
  Custom = "Custom"
}

export enum NotificationChannel {
  Email = "Email",
  Webhook = "Webhook",
  SMS = "SMS",
  Slack = "Slack"
}

export interface AlertRule {
  id: number;
  name: string;
  description?: string;
  type: AlertRuleType;
  serverId?: number;
  serverName?: string;
  metricName?: string;
  condition: 'gt' | 'lt' | 'eq' | 'ne';
  threshold: number;
  severity: AlertRuleSeverity;
  enabled: boolean;
  cooldownMinutes: number;
  notificationChannels: NotificationChannel[];
  webhookUrl?: string;
  emailRecipients?: string[];
  slackWebhookUrl?: string;
  createdAt: string;
  lastTriggered?: string;
}

export interface Alert {
  id: number;
  ruleId: number;
  ruleName: string;
  serverId?: number;
  serverName?: string;
  type: AlertRuleType;
  severity: AlertRuleSeverity;
  status: AlertRuleStatus;
  message: string;
  metricValue?: number;
  threshold?: number;
  acknowledgedBy?: string;
  acknowledgedAt?: string;
  resolvedAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AlertStats {
  totalAlerts: number;
  activeAlerts: number;
  criticalAlerts: number;
  warningAlerts: number;
  acknowledgedAlerts: number;
  resolvedToday: number;
}

export interface CreateAlertRuleRequest {
  name: string;
  description?: string;
  type: AlertRuleType;
  serverId?: number;
  metricName?: string;
  condition: 'gt' | 'lt' | 'eq' | 'ne';
  threshold: number;
  severity: AlertRuleSeverity;
  cooldownMinutes: number;
  notificationChannels: NotificationChannel[];
  webhookUrl?: string;
  emailRecipients?: string[];
  slackWebhookUrl?: string;
}

export interface UpdateAlertRuleRequest {
  name?: string;
  description?: string;
  threshold?: number;
  severity?: AlertRuleSeverity;
  enabled?: boolean;
  cooldownMinutes?: number;
  notificationChannels?: NotificationChannel[];
  webhookUrl?: string;
  emailRecipients?: string[];
  slackWebhookUrl?: string;
}

export interface AcknowledgeAlertRequest {
  alertIds: number[];
  comment?: string;
}

export interface AlertHistory {
  id: number;
  alertId: number;
  action: string;
  oldStatus: AlertRuleStatus;
  newStatus: AlertRuleStatus;
  description?: string;
  timestamp: string;
  performedBy: string;
}

export interface AlertComment {
  id: number;
  alertId: number;
  comment: string;
  commentType: string;
  createdAt: string;
  createdBy: string;
}

export interface CommentRequest {
  comment: string;
  commentType?: string;
  user?: string;
}