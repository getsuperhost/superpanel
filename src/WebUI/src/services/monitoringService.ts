import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { ServerMetrics, ServerAlert } from '../types/monitoring';

class MonitoringService {
  private connection: HubConnection | null = null;

  // Callbacks for handling real-time updates
  private onMetricsUpdate?: (serverId: number, metrics: ServerMetrics) => void;
  private onAlertReceived?: (alert: ServerAlert) => void;
  private onConnectionStatusChange?: (connected: boolean) => void;

  constructor() {
    this.initializeConnection();
  }

  private initializeConnection() {
    const token = localStorage.getItem('authToken');
    if (!token) {
      console.warn('No auth token available for SignalR connection');
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('http://localhost:7001/hubs/monitoring', {
        accessTokenFactory: () => token,
        transport: 1 | 2 | 4, // WebSockets | ServerSentEvents | LongPolling
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.setupEventHandlers();
  }

  private setupEventHandlers() {
    if (!this.connection) return;

    this.connection.on('ReceiveServerMetrics', (serverId: number, metrics: ServerMetrics) => {
      console.log(`Received metrics for server ${serverId}:`, metrics);
      this.onMetricsUpdate?.(serverId, metrics);
    });

    this.connection.on('ReceiveAlert', (alert: ServerAlert) => {
      console.log('Received alert:', alert);
      this.onAlertReceived?.(alert);
    });

    this.connection.onclose(() => {
      console.log('SignalR connection closed');
      this.onConnectionStatusChange?.(false);
    });

    this.connection.onreconnected(() => {
      console.log('SignalR connection reconnected');
      this.onConnectionStatusChange?.(true);
    });
  }

  async start(): Promise<void> {
    if (!this.connection) {
      this.initializeConnection();
    }

    if (!this.connection) {
      throw new Error('Failed to initialize SignalR connection');
    }

    // Don't start if already connecting or connected
    if (this.connection.state !== 'Disconnected') {
      console.log('SignalR connection already started or connecting');
      return;
    }

    try {
      await this.connection.start();
      console.log('SignalR connection started successfully');
      this.onConnectionStatusChange?.(true);
    } catch (error) {
      console.error('Failed to start SignalR connection:', error);
      this.onConnectionStatusChange?.(false);
      throw error;
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      console.log('SignalR connection stopped');
      this.onConnectionStatusChange?.(false);
    }
  }

  async subscribeToServer(serverId: number): Promise<void> {
    if (!this.connection) {
      throw new Error('Connection not initialized');
    }

    try {
      await this.connection.invoke('SubscribeToServerMetrics', serverId);
      console.log(`Subscribed to metrics for server ${serverId}`);
    } catch (error) {
      console.error(`Failed to subscribe to server ${serverId}:`, error);
      throw error;
    }
  }

  async unsubscribeFromServer(serverId: number): Promise<void> {
    if (!this.connection) {
      throw new Error('Connection not initialized');
    }

    try {
      await this.connection.invoke('UnsubscribeFromServerMetrics', serverId);
      console.log(`Unsubscribed from metrics for server ${serverId}`);
    } catch (error) {
      console.error(`Failed to unsubscribe from server ${serverId}:`, error);
      throw error;
    }
  }

  // Set callback functions
  setOnMetricsUpdate(callback: (serverId: number, metrics: ServerMetrics) => void) {
    this.onMetricsUpdate = callback;
  }

  setOnAlertReceived(callback: (alert: ServerAlert) => void) {
    this.onAlertReceived = callback;
  }

  setOnConnectionStatusChange(callback: (connected: boolean) => void) {
    this.onConnectionStatusChange = callback;
  }

  isConnected(): boolean {
    return this.connection?.state === 'Connected';
  }

  // Cleanup method
  dispose() {
    this.stop();
    this.connection = null;
  }
}

// Export singleton instance
export const monitoringService = new MonitoringService();
export default monitoringService;