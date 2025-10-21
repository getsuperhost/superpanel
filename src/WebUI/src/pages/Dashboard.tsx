import React, { useState, useEffect } from 'react';
import { Card, Row, Col, Statistic, Typography, Spin, Alert, Badge, Tag, List, Button } from 'antd';
import {
  CloudServerOutlined,
  GlobalOutlined,
  DatabaseOutlined,
  FolderOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
  WifiOutlined,
  ClearOutlined,
} from '@ant-design/icons';
import { dashboardApi } from '../services/api';
import { DashboardStats } from '../types/dashboard';
import { useMonitoring } from '../hooks/useMonitoring';
import { AlertSeverity } from '../types/monitoring';

const { Title } = Typography;

const Dashboard: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Real-time monitoring
  const {
    metrics,
    alerts,
    isConnected,
    subscribeToServer,
    clearAlerts,
    getRecentAlerts
  } = useMonitoring();

  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        setLoading(true);
        setError(null);
        
        const dashboardStats = await dashboardApi.getStats();
        setStats(dashboardStats);
      } catch (err) {
        console.error('Dashboard API error:', err);
        setError(err instanceof Error ? err.message : 'Failed to load dashboard data');
        
        // Fallback to mock data if API fails
        setStats({
          totalServers: 3,
          runningServers: 2,
          activeDomains: 12,
          totalDomains: 15,
          totalDatabases: 8,
          activeDatabases: 7,
          systemInfo: {
            serverName: 'SuperPanel-Server',
            operatingSystem: 'Linux',
            architecture: 'x64',
            cpuUsagePercent: 23.5,
            totalMemoryMB: 8192,
            availableMemoryMB: 5400,
            drives: [
              {
                name: '/',
                fileSystem: 'ext4',
                totalSizeGB: 500,
                availableSpaceGB: 290,
                usagePercent: 42.0
              }
            ],
            topProcesses: [],
            lastUpdated: new Date().toISOString()
          }
        });
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, []);

  // Handle SignalR subscriptions when connection is established
  useEffect(() => {
    const handleSubscription = async () => {
      if (isConnected && stats) {
        try {
          // Subscribe to real-time monitoring for all servers
          for (let i = 1; i <= stats.totalServers; i++) {
            await subscribeToServer(i);
          }
        } catch (error) {
          console.error('Failed to subscribe to servers:', error);
        }
      }
    };

    handleSubscription();
  }, [isConnected, stats, subscribeToServer]);

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: '50px' }}>
        <Spin size="large" />
        <div style={{ marginTop: '16px' }}>Loading dashboard...</div>
      </div>
    );
  }

  if (error && !stats) {
    return (
      <Alert
        message="Connection Error"
        description="Unable to connect to API. Using fallback data for demonstration."
        type="warning"
        showIcon
        style={{ margin: '20px 0' }}
      />
    );
  }

  if (!stats) {
    return null; // This shouldn't happen with our current logic
  }

  const memoryUsagePercent = Math.round((1 - stats.systemInfo.availableMemoryMB / stats.systemInfo.totalMemoryMB) * 100);
  const diskUsagePercent = stats.systemInfo.drives.length > 0 ? stats.systemInfo.drives[0].usagePercent : 0;
  const totalStorageGB = stats.systemInfo.drives.reduce((total, drive) => total + drive.totalSizeGB, 0);
  const usedStorageGB = stats.systemInfo.drives.reduce((total, drive) => total + (drive.totalSizeGB - drive.availableSpaceGB), 0);

  return (
    <div>
      <Title level={2}>
        Dashboard
        <Badge
          status={isConnected ? "success" : "error"}
          text={isConnected ? "Real-time monitoring active" : "Real-time monitoring offline"}
          style={{ marginLeft: '16px' }}
        />
      </Title>
      
      {error && (
        <Alert
          message="API Connection Issue"
          description="Some data may not be current due to API connectivity issues."
          type="warning"
          showIcon
          style={{ marginBottom: '16px' }}
          closable
        />
      )}

      {/* Real-time Alerts */}
      {alerts.length > 0 && (
        <Alert
          message={`Active Alerts (${alerts.length})`}
          description={
            <div>
              <List
                size="small"
                dataSource={getRecentAlerts(5)}
                renderItem={(alert) => (
                  <List.Item>
                    <Tag color={
                      alert.severity === AlertSeverity.Critical ? 'red' :
                      alert.severity === AlertSeverity.Warning ? 'orange' : 'blue'
                    }>
                      {alert.severity}
                    </Tag>
                    <strong>{alert.serverName}:</strong> {alert.message}
                    <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
                      {new Date(alert.timestamp).toLocaleTimeString()}
                    </div>
                  </List.Item>
                )}
              />
              {alerts.length > 5 && (
                <div style={{ marginTop: '8px' }}>
                  <Button size="small" onClick={clearAlerts} icon={<ClearOutlined />}>
                    Clear All Alerts
                  </Button>
                </div>
              )}
            </div>
          }
          type="warning"
          showIcon
          style={{ marginBottom: '16px' }}
          closable={false}
        />
      )}
      
      {/* Overview Cards */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Servers"
              value={stats.totalServers}
              prefix={<CloudServerOutlined />}
              valueStyle={{ color: '#3f8600' }}
            />
            <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
              {stats.runningServers} running
            </div>
            {isConnected && (
              <div style={{ fontSize: '12px', color: '#52c41a', marginTop: '4px' }}>
                <WifiOutlined /> Live monitoring
              </div>
            )}
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Domains"
              value={stats.activeDomains}
              prefix={<GlobalOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
            <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
              {stats.totalDomains} total domains
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Databases"
              value={stats.activeDatabases}
              prefix={<DatabaseOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
            <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
              {stats.totalDatabases} total databases
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Storage Used"
              value={usedStorageGB}
              suffix={`/${totalStorageGB}GB`}
              prefix={<FolderOutlined />}
              valueStyle={{ color: '#fa8c16' }}
            />
          </Card>
        </Col>
      </Row>

      {/* System Performance */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={8}>
          <Card title="CPU Usage">
            <Statistic
              value={stats.systemInfo.cpuUsagePercent}
              precision={1}
              suffix="%"
              valueStyle={{ 
                color: stats.systemInfo.cpuUsagePercent > 80 ? '#cf1322' : 
                       stats.systemInfo.cpuUsagePercent > 60 ? '#fa8c16' : '#3f8600' 
              }}
              prefix={stats.systemInfo.cpuUsagePercent > 50 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card title="Memory Usage">
            <Statistic
              value={memoryUsagePercent}
              precision={1}
              suffix="%"
              valueStyle={{ 
                color: memoryUsagePercent > 80 ? '#cf1322' : 
                       memoryUsagePercent > 60 ? '#fa8c16' : '#3f8600' 
              }}
              prefix={memoryUsagePercent > 50 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
            />
            <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
              {Math.round(stats.systemInfo.availableMemoryMB / 1024)}GB / {Math.round(stats.systemInfo.totalMemoryMB / 1024)}GB
            </div>
          </Card>
        </Col>
        <Col xs={24} sm={8}>
          <Card title="Disk Usage">
            <Statistic
              value={diskUsagePercent}
              precision={1}
              suffix="%"
              valueStyle={{ 
                color: diskUsagePercent > 80 ? '#cf1322' : 
                       diskUsagePercent > 60 ? '#fa8c16' : '#3f8600' 
              }}
              prefix={diskUsagePercent > 50 ? <ArrowUpOutlined /> : <ArrowDownOutlined />}
            />
          </Card>
        </Col>
      </Row>

      {/* Real-time Server Metrics */}
      {isConnected && Object.keys(metrics).length > 0 && (
        <Row gutter={[16, 16]} style={{ marginTop: '24px' }}>
          <Col span={24}>
            <Card title="Real-time Server Metrics" extra={
              <Badge status="success" text="Live" />
            }>
              <Row gutter={[16, 16]}>
                {Object.entries(metrics).map(([serverId, serverMetrics]) => (
                  <Col xs={24} sm={12} lg={8} key={serverId}>
                    <Card size="small" title={`Server ${serverId}`}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                        <span>CPU:</span>
                        <span style={{
                          color: serverMetrics.cpuUsage > 80 ? '#cf1322' :
                                 serverMetrics.cpuUsage > 60 ? '#fa8c16' : '#3f8600',
                          fontWeight: 'bold'
                        }}>
                          {serverMetrics.cpuUsage.toFixed(1)}%
                        </span>
                      </div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                        <span>Memory:</span>
                        <span style={{
                          color: serverMetrics.memoryUsage > 80 ? '#cf1322' :
                                 serverMetrics.memoryUsage > 60 ? '#fa8c16' : '#3f8600',
                          fontWeight: 'bold'
                        }}>
                          {serverMetrics.memoryUsage.toFixed(1)}%
                        </span>
                      </div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                        <span>Disk:</span>
                        <span style={{
                          color: serverMetrics.diskUsage > 90 ? '#cf1322' :
                                 serverMetrics.diskUsage > 80 ? '#fa8c16' : '#3f8600',
                          fontWeight: 'bold'
                        }}>
                          {serverMetrics.diskUsage.toFixed(1)}%
                        </span>
                      </div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                        <span>Connections:</span>
                        <span style={{ fontWeight: 'bold' }}>
                          {serverMetrics.activeConnections}
                        </span>
                      </div>
                      <div style={{ fontSize: '12px', color: '#666', textAlign: 'center', marginTop: '8px' }}>
                        Updated: {new Date(serverMetrics.timestamp).toLocaleTimeString()}
                      </div>
                    </Card>
                  </Col>
                ))}
              </Row>
            </Card>
          </Col>
        </Row>
      )}

      {/* Quick Actions */}
      <Row gutter={[16, 16]} style={{ marginTop: '24px' }}>
        <Col span={24}>
          <Card title="Quick Actions">
            <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
              <Card size="small" style={{ cursor: 'pointer' }} onClick={() => window.location.href = '/servers'}>
                <div style={{ textAlign: 'center' }}>
                  <CloudServerOutlined style={{ fontSize: '24px', marginBottom: '8px' }} />
                  <div>Manage Servers</div>
                </div>
              </Card>
              <Card size="small" style={{ cursor: 'pointer' }} onClick={() => window.location.href = '/domains'}>
                <div style={{ textAlign: 'center' }}>
                  <GlobalOutlined style={{ fontSize: '24px', marginBottom: '8px' }} />
                  <div>Add Domain</div>
                </div>
              </Card>
              <Card size="small" style={{ cursor: 'pointer' }} onClick={() => window.location.href = '/files'}>
                <div style={{ textAlign: 'center' }}>
                  <FolderOutlined style={{ fontSize: '24px', marginBottom: '8px' }} />
                  <div>File Manager</div>
                </div>
              </Card>
              <Card size="small" style={{ cursor: 'pointer' }} onClick={() => window.location.href = '/monitoring'}>
                <div style={{ textAlign: 'center' }}>
                  <DatabaseOutlined style={{ fontSize: '24px', marginBottom: '8px' }} />
                  <div>View Monitoring</div>
                </div>
              </Card>
            </div>
          </Card>
        </Col>
      </Row>
    </div>
  );
};

export default Dashboard;