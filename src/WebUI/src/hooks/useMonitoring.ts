import { useState, useEffect, useCallback } from 'react';
import { ServerMetrics, ServerAlert, MonitoringState } from '../types/monitoring';
import monitoringService from '../services/monitoringService';

export const useMonitoring = () => {
  const [state, setState] = useState<MonitoringState>({
    metrics: {},
    alerts: [],
    isConnected: false,
    subscribedServers: []
  });

  // Initialize monitoring service callbacks
  useEffect(() => {
    monitoringService.setOnMetricsUpdate((serverId: number, metrics: ServerMetrics) => {
      setState(prev => ({
        ...prev,
        metrics: {
          ...prev.metrics,
          [serverId]: metrics
        }
      }));
    });

    monitoringService.setOnAlertReceived((alert: ServerAlert) => {
      setState(prev => ({
        ...prev,
        alerts: [alert, ...prev.alerts].slice(0, 50) // Keep last 50 alerts
      }));
    });

    monitoringService.setOnConnectionStatusChange((connected: boolean) => {
      setState(prev => ({
        ...prev,
        isConnected: connected
      }));
    });

    // Start connection when component mounts
    const startConnection = async () => {
      try {
        await monitoringService.start();
      } catch (error) {
        console.error('Failed to start monitoring connection:', error);
      }
    };

    startConnection();

    // Cleanup on unmount
    return () => {
      monitoringService.dispose();
    };
  }, []);

  const subscribeToServer = useCallback(async (serverId: number) => {
    try {
      await monitoringService.subscribeToServer(serverId);
      setState(prev => ({
        ...prev,
        subscribedServers: [...prev.subscribedServers, serverId]
      }));
    } catch (error) {
      console.error(`Failed to subscribe to server ${serverId}:`, error);
    }
  }, []);

  const unsubscribeFromServer = useCallback(async (serverId: number) => {
    try {
      await monitoringService.unsubscribeFromServer(serverId);
      setState(prev => ({
        ...prev,
        subscribedServers: prev.subscribedServers.filter(id => id !== serverId)
      }));
    } catch (error) {
      console.error(`Failed to unsubscribe from server ${serverId}:`, error);
    }
  }, []);

  const clearAlerts = useCallback(() => {
    setState(prev => ({
      ...prev,
      alerts: []
    }));
  }, []);

  const getServerMetrics = useCallback((serverId: number): ServerMetrics | null => {
    return state.metrics[serverId] || null;
  }, [state.metrics]);

  const getRecentAlerts = useCallback((limit: number = 10): ServerAlert[] => {
    return state.alerts.slice(0, limit);
  }, [state.alerts]);

  return {
    // State
    metrics: state.metrics,
    alerts: state.alerts,
    isConnected: state.isConnected,
    subscribedServers: state.subscribedServers,

    // Actions
    subscribeToServer,
    unsubscribeFromServer,
    clearAlerts,

    // Getters
    getServerMetrics,
    getRecentAlerts
  };
};