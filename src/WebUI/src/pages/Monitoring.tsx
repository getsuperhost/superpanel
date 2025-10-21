import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Row, Col, Statistic, Progress, Alert, List, Tag, Spin } from 'antd';
import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';
import { serverApi } from '../services/api';
import { Server, ServerStatus } from '../services/api';

interface ServerMetrics {
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  networkIn: number;
  networkOut: number;
  activeConnections: number;
  timestamp: string;
  status: ServerStatus;
}

interface ServerAlert {
  serverId: number;
  serverName: string;
  type: string;
  message: string;
  severity: 'Info' | 'Warning' | 'Critical';
  timestamp: string;
}

const Monitoring: React.FC = () => {
  const [servers, setServers] = useState<Server[]>([]);
  const [metrics, setMetrics] = useState<Record<number, ServerMetrics>>({});
  const [alerts, setAlerts] = useState<ServerAlert[]>([]);
  const [loading, setLoading] = useState(true);
  const connectionRef = useRef<HubConnection | null>(null);

  const loadServers = async () => {
    try {
      const data = await serverApi.getAll();
      setServers(data);
    } catch (error) {
      console.error('Failed to load servers:', error);
    } finally {
      setLoading(false);
    }
  };

  const setupSignalRConnection = useCallback(async () => {
    try {
      const connection = new HubConnectionBuilder()
        .withUrl('/hubs/monitoring', {
          accessTokenFactory: () => localStorage.getItem('authToken') || ''
        })
        .withAutomaticReconnect()
        .build();

      connection.on('ReceiveServerMetrics', (serverId: number, serverMetrics: ServerMetrics) => {
        setMetrics(prev => ({
          ...prev,
          [serverId]: serverMetrics
        }));
      });

      connection.on('ReceiveAlert', (alert: ServerAlert) => {
        setAlerts(prev => [alert, ...prev.slice(0, 9)]); // Keep last 10 alerts
      });

      await connection.start();
      connectionRef.current = connection;

      // Subscribe to all servers
      servers.forEach(server => {
        connection.invoke('SubscribeToServerMetrics', server.id);
      });
    } catch (error) {
      console.error('SignalR connection failed:', error);
    }
  }, [servers]);

  useEffect(() => {
    loadServers();
    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
  }, []);

  useEffect(() => {
    if (servers.length > 0 && !connectionRef.current) {
      setupSignalRConnection();
    }
  }, [servers, setupSignalRConnection]);

  const getStatusColor = (status: ServerStatus) => {
    switch (status) {
      case 'Running': return 'green';
      case 'Stopped': return 'red';
      case 'Error': return 'red';
      case 'Maintenance': return 'orange';
      default: return 'gray';
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '200px' }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div>
      <h1>System Monitoring</h1>

      {/* Alerts Section */}
      {alerts.length > 0 && (
        <Card title="Recent Alerts" style={{ marginBottom: 16 }}>
          <List
            dataSource={alerts}
            renderItem={(alert) => (
              <List.Item>
                <Alert
                  message={`${alert.serverName}: ${alert.message}`}
                  type={alert.severity === 'Critical' ? 'error' : alert.severity === 'Warning' ? 'warning' : 'info'}
                  showIcon
                  style={{ width: '100%' }}
                />
              </List.Item>
            )}
          />
        </Card>
      )}

      {/* Server Metrics */}
      <Row gutter={[16, 16]}>
        {servers.map((server) => {
          const serverMetrics = metrics[server.id];
          return (
            <Col xs={24} lg={12} xl={8} key={server.id}>
              <Card
                title={
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span>{server.name}</span>
                    <Tag color={getStatusColor(serverMetrics?.status || server.status)}>
                      {serverMetrics?.status || server.status}
                    </Tag>
                  </div>
                }
                size="small"
              >
                {serverMetrics ? (
                  <>
                    <Row gutter={[8, 8]}>
                      <Col span={12}>
                        <Statistic
                          title="CPU Usage"
                          value={serverMetrics.cpuUsage}
                          suffix="%"
                          valueStyle={{ color: serverMetrics.cpuUsage > 80 ? '#cf1322' : '#3f8600' }}
                        />
                        <Progress
                          percent={serverMetrics.cpuUsage}
                          size="small"
                          status={serverMetrics.cpuUsage > 90 ? 'exception' : 'active'}
                        />
                      </Col>
                      <Col span={12}>
                        <Statistic
                          title="Memory Usage"
                          value={serverMetrics.memoryUsage}
                          suffix="%"
                          valueStyle={{ color: serverMetrics.memoryUsage > 80 ? '#cf1322' : '#3f8600' }}
                        />
                        <Progress
                          percent={serverMetrics.memoryUsage}
                          size="small"
                          status={serverMetrics.memoryUsage > 90 ? 'exception' : 'active'}
                        />
                      </Col>
                    </Row>
                    <Row gutter={[8, 8]} style={{ marginTop: 8 }}>
                      <Col span={12}>
                        <Statistic
                          title="Disk Usage"
                          value={serverMetrics.diskUsage}
                          suffix="%"
                          valueStyle={{ color: serverMetrics.diskUsage > 90 ? '#cf1322' : '#3f8600' }}
                        />
                        <Progress
                          percent={serverMetrics.diskUsage}
                          size="small"
                          status={serverMetrics.diskUsage > 95 ? 'exception' : 'active'}
                        />
                      </Col>
                      <Col span={12}>
                        <Statistic
                          title="Active Connections"
                          value={serverMetrics.activeConnections}
                        />
                      </Col>
                    </Row>
                    <Row gutter={[8, 8]} style={{ marginTop: 8 }}>
                      <Col span={12}>
                        <Statistic
                          title="Network In"
                          value={formatBytes(serverMetrics.networkIn)}
                          suffix="/s"
                        />
                      </Col>
                      <Col span={12}>
                        <Statistic
                          title="Network Out"
                          value={formatBytes(serverMetrics.networkOut)}
                          suffix="/s"
                        />
                      </Col>
                    </Row>
                    <div style={{ marginTop: 8, fontSize: '12px', color: '#666' }}>
                      Last updated: {new Date(serverMetrics.timestamp).toLocaleTimeString()}
                    </div>
                  </>
                ) : (
                  <div style={{ textAlign: 'center', padding: '20px' }}>
                    <Spin size="small" />
                    <div style={{ marginTop: 8 }}>Waiting for metrics...</div>
                  </div>
                )}
              </Card>
            </Col>
          );
        })}
      </Row>
    </div>
  );
};

export default Monitoring;