import React, { useState, useEffect } from 'react';
import {
  Card,
  Typography,
  Table,
  Button,
  Modal,
  Form,
  Input,
  InputNumber,
  Select,
  Tag,
  Space,
  Popconfirm,
  message,
  Alert,
  Spin,
  Badge,
  Tooltip,
  Row,
  Col,
  Statistic,
  Tabs,
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  GlobalOutlined,
  LockOutlined,
  UnlockOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { domainApi, serverApi } from '../services/api';
import { Domain, Server, DomainStatus, DnsRecord, DnsRecordType, DnsRecordStatus, DnsPropagationStatus } from '../types/domains';

const { Title } = Typography;

const Domains: React.FC = () => {
  const [domains, setDomains] = useState<Domain[]>([]);
  const [servers, setServers] = useState<Server[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingDomain, setEditingDomain] = useState<Domain | null>(null);
  const [form] = Form.useForm();
  const [error, setError] = useState<string | null>(null);

  // DNS Records state
  const [dnsRecords, setDnsRecords] = useState<DnsRecord[]>([]);
  const [dnsModalVisible, setDnsModalVisible] = useState(false);
  const [editingDnsRecord, setEditingDnsRecord] = useState<DnsRecord | null>(null);
  const [selectedDomain, setSelectedDomain] = useState<Domain | null>(null);
  const [dnsForm] = Form.useForm();

  // DNS Zone File state
  const [zoneFile, setZoneFile] = useState<string>('');
  const [zoneFileLoading, setZoneFileLoading] = useState(false);

  // DNS Propagation state
  const [propagationStatus, setPropagationStatus] = useState<DnsPropagationStatus | null>(null);
  const [propagationLoading, setPropagationLoading] = useState(false);

  // DNS status tracking for main table
  const [dnsStatusMap, setDnsStatusMap] = useState<Record<number, { recordCount: number; propagationStatus: DnsPropagationStatus | null; error?: boolean }>>({});
  const [dnsStatusLoading, setDnsStatusLoading] = useState<Record<number, boolean>>({});

  // Load domains and servers on component mount
  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      // Load domains and servers in parallel
      const [domainsData, serversData] = await Promise.all([
        domainApi.getAll(),
        serverApi.getAll()
      ]);

      setDomains(domainsData);
      setServers(serversData);

      // Load DNS status for all domains
      if (domainsData.length > 0) {
        await loadDnsStatusForAllDomains(domainsData);
      }
    } catch (err) {
      console.error('Failed to load data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load domains');
    } finally {
      setLoading(false);
    }
  };

  const loadDnsRecords = async (domainId: number) => {
    try {
      const records = await domainApi.getDnsRecords(domainId);
      setDnsRecords(records);
    } catch (err) {
      console.error('Failed to load DNS records:', err);
      message.error('Failed to load DNS records');
    }
  };

  const loadZoneFile = async (domainId: number) => {
    try {
      setZoneFileLoading(true);
      const zone = await domainApi.getDnsZone(domainId);
      setZoneFile(zone.zoneFile || '');
    } catch (err) {
      console.error('Failed to load zone file:', err);
      message.error('Failed to load zone file');
    } finally {
      setZoneFileLoading(false);
    }
  };

  const loadPropagationStatus = async (domainId: number) => {
    try {
      setPropagationLoading(true);
      const status = await domainApi.getDnsPropagationStatus(domainId);
      setPropagationStatus(status);
    } catch (err) {
      console.error('Failed to load propagation status:', err);
      message.error('Failed to load propagation status');
    } finally {
      setPropagationLoading(false);
    }
  };

  const checkPropagation = async (domainId: number) => {
    try {
      setPropagationLoading(true);
      await domainApi.checkDnsPropagation(domainId);
      message.success('Propagation check initiated');
      // Reload status after a short delay
      setTimeout(() => loadPropagationStatus(domainId), 2000);
    } catch (err) {
      console.error('Failed to check propagation:', err);
      message.error('Failed to check propagation');
    } finally {
      setPropagationLoading(false);
    }
  };

  const loadDnsStatusForDomain = async (domainId: number) => {
    try {
      setDnsStatusLoading(prev => ({ ...prev, [domainId]: true }));
      const records = await domainApi.getDnsRecords(domainId);
      const propagationStatus = await domainApi.getDnsPropagationStatus(domainId);
      
      setDnsStatusMap(prev => ({
        ...prev,
        [domainId]: {
          recordCount: records.length,
          propagationStatus: propagationStatus
        }
      }));
    } catch (err) {
      console.error('Failed to load DNS status for domain:', err);
      // Set error state
      setDnsStatusMap(prev => ({
        ...prev,
        [domainId]: {
          recordCount: 0,
          propagationStatus: null,
          error: true
        }
      }));
    } finally {
      setDnsStatusLoading(prev => ({ ...prev, [domainId]: false }));
    }
  };

  const loadDnsStatusForAllDomains = async (domains: Domain[]) => {
    // Load DNS status for all domains with concurrency control
    const concurrencyLimit = 5; // Load 5 domains at a time
    for (let i = 0; i < domains.length; i += concurrencyLimit) {
      const batch = domains.slice(i, i + concurrencyLimit);
      await Promise.all(batch.map(domain => loadDnsStatusForDomain(domain.id)));
    }
  };

  const handleCreateDomain = () => {
    setEditingDomain(null);
    form.resetFields();
    setModalVisible(true);
  };

  const handleEditDomain = (domain: Domain) => {
    setEditingDomain(domain);
    form.setFieldsValue({
      name: domain.name,
      serverId: domain.serverId,
      documentRoot: domain.documentRoot,
      status: domain.status,
      sslEnabled: domain.sslEnabled,
    });
    setModalVisible(true);
  };

  const handleDeleteDomain = async (id: number) => {
    try {
      await domainApi.delete(id);
      message.success('Domain deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete domain');
      console.error('Delete domain error:', err);
    }
  };

  // DNS Record handlers
  const handleManageDns = (domain: Domain) => {
    setSelectedDomain(domain);
    loadDnsRecords(domain.id);
    loadPropagationStatus(domain.id);
    setDnsModalVisible(true);
  };

  const handleCreateDnsRecord = () => {
    setEditingDnsRecord(null);
    dnsForm.resetFields();
  };

  const handleEditDnsRecord = (record: DnsRecord) => {
    setEditingDnsRecord(record);
    dnsForm.setFieldsValue({
      type: record.type,
      name: record.name,
      value: record.value,
      ttl: record.ttl,
      priority: record.priority,
    });
  };

  const handleDeleteDnsRecord = async (recordId: number) => {
    if (!selectedDomain) return;
    try {
      await domainApi.deleteDnsRecord(selectedDomain.id, recordId);
      message.success('DNS record deleted successfully');
      loadDnsRecords(selectedDomain.id);
    } catch (err) {
      message.error('Failed to delete DNS record');
      console.error('Delete DNS record error:', err);
    }
  };

  const handleDnsModalOk = async () => {
    if (!selectedDomain) return;
    try {
      const values = await dnsForm.validateFields();

      if (editingDnsRecord) {
        // Update existing DNS record
        await domainApi.updateDnsRecord(selectedDomain.id, editingDnsRecord.id, values);
        message.success('DNS record updated successfully');
      } else {
        // Create new DNS record
        await domainApi.createDnsRecord(selectedDomain.id, values);
        message.success('DNS record created successfully');
      }

      setDnsModalVisible(false);
      loadDnsRecords(selectedDomain.id);
    } catch (err) {
      message.error('Failed to save DNS record');
      console.error('Save DNS record error:', err);
    }
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();

      // Convert date to ISO string if present
      if (values.sslExpiry) {
        values.sslExpiry = values.sslExpiry.toISOString();
      }

      if (editingDomain) {
        // Update existing domain
        await domainApi.update(editingDomain.id, values);
        message.success('Domain updated successfully');
      } else {
        // Create new domain
        await domainApi.create(values);
        message.success('Domain created successfully');
      }

      setModalVisible(false);
      loadData();
    } catch (err) {
      if (err instanceof Error && err.message.includes('API Error')) {
        message.error('Failed to save domain - API not available');
      } else {
        message.error('Please check all required fields');
      }
      console.error('Save domain error:', err);
    }
  };

  const getSSLStatus = (domain: Domain) => {
    if (!domain.sslEnabled) {
      return { status: 'No SSL', color: 'default', icon: <UnlockOutlined /> };
    }

    return { status: 'SSL Enabled', color: 'green', icon: <LockOutlined /> };
  };

  const getServerName = (serverId: number) => {
    const server = servers.find(s => s.id === serverId);
    return server ? server.name : `Server ${serverId}`;
  };

  const columns: ColumnsType<Domain> = [
    {
      title: 'Domain Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record: Domain) => {
        const dnsStatus = dnsStatusMap[record.id];
        const hasDnsIssues = dnsStatus?.propagationStatus?.overallStatus === 'error';
        const isPropagating = dnsStatus?.propagationStatus?.overallStatus === 'propagating';
        
        return (
          <Space>
            <Space>
              {dnsStatus && (
                <Tooltip title={`DNS: ${dnsStatus.recordCount} records, ${dnsStatus.propagationStatus?.overallStatus || 'unknown'}`}>
                  <Badge
                    status={
                      hasDnsIssues ? 'error' :
                      isPropagating ? 'processing' :
                      dnsStatus.propagationStatus?.overallStatus === 'propagated' ? 'success' : 'default'
                    }
                    style={{ marginRight: 4 }}
                  />
                </Tooltip>
              )}
              <GlobalOutlined style={{ color: '#1890ff' }} />
            </Space>
            <a href={`http://${name}`} target="_blank" rel="noopener noreferrer">
              {name}
            </a>
          </Space>
        );
      },
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: 'Server',
      dataIndex: 'serverId',
      key: 'serverId',
      render: (serverId: number) => getServerName(serverId),
      filters: servers.map(server => ({ text: server.name, value: server.id })),
      onFilter: (value, record) => record.serverId === value,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: DomainStatus) => (
        <Tag color={status === DomainStatus.Active ? 'green' : status === DomainStatus.Inactive ? 'red' : 'orange'}>
          {DomainStatus[status]}
        </Tag>
      ),
      filters: [
        { text: 'Active', value: DomainStatus.Active },
        { text: 'Inactive', value: DomainStatus.Inactive },
        { text: 'Suspended', value: DomainStatus.Suspended },
        { text: 'Expired', value: DomainStatus.Expired },
      ],
      onFilter: (value, record) => record.status === value,
    },
    {
      title: 'SSL Certificate',
      key: 'ssl',
      render: (_, record: Domain) => {
        const sslStatus = getSSLStatus(record);
        return (
          <Tooltip title={sslStatus.status}>
            <Badge
              status={sslStatus.color === 'green' ? 'success' : sslStatus.color === 'red' ? 'error' : 'default'}
              text={
                <Space>
                  {sslStatus.icon}
                  {sslStatus.status}
                </Space>
              }
            />
          </Tooltip>
        );
      },
    },
    {
      title: 'DNS Status',
      key: 'dnsStatus',
      render: (_, record: Domain) => {
        const dnsStatus = dnsStatusMap[record.id];
        const isLoading = dnsStatusLoading[record.id];
        
        if (isLoading) {
          return <Spin size="small" />;
        }
        
        if (!dnsStatus || dnsStatus.error) {
          return (
            <Button
              type="link"
              size="small"
              onClick={() => loadDnsStatusForDomain(record.id)}
              style={{ padding: 0 }}
            >
              Load DNS Status
            </Button>
          );
        }
        
        const propagationColor =
          dnsStatus.propagationStatus?.overallStatus === 'propagated' ? 'green' :
          dnsStatus.propagationStatus?.overallStatus === 'propagating' ? 'orange' : 'red';
        
        return (
          <Space direction="vertical" size={0}>
            <div>
              <GlobalOutlined style={{ marginRight: 4 }} />
              {dnsStatus.recordCount} records
            </div>
            {dnsStatus.propagationStatus && (
              <Tag color={propagationColor}>
                {dnsStatus.propagationStatus.overallStatus}
              </Tag>
            )}
          </Space>
        );
      },
    },
    {
      title: 'Document Root',
      dataIndex: 'documentRoot',
      key: 'documentRoot',
      responsive: ['lg'],
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleDateString(),
      sorter: (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
      responsive: ['md'],
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: Domain) => (
        <Space size="small">
          <Tooltip title="Edit Domain">
            <Button
              type="text"
              icon={<EditOutlined />}
              onClick={() => handleEditDomain(record)}
            />
          </Tooltip>
          <Tooltip title={
            dnsStatusMap[record.id] 
              ? `Manage DNS Records (${dnsStatusMap[record.id].recordCount} records, ${dnsStatusMap[record.id].propagationStatus?.overallStatus || 'unknown'})`
              : 'Manage DNS Records'
          }>
            <Button
              type={dnsStatusMap[record.id]?.propagationStatus?.overallStatus === 'error' ? 'primary' : 'text'}
              danger={dnsStatusMap[record.id]?.propagationStatus?.overallStatus === 'error'}
              icon={<GlobalOutlined />}
              onClick={() => handleManageDns(record)}
            />
          </Tooltip>
          <Tooltip title="Domain Settings">
            <Button
              type="text"
              icon={<SettingOutlined />}
              onClick={() => message.info('Domain settings coming soon')}
            />
          </Tooltip>
          <Popconfirm
            title="Delete Domain"
            description="Are you sure you want to delete this domain?"
            onConfirm={() => handleDeleteDomain(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Tooltip title="Delete Domain">
              <Button
                type="text"
                danger
                icon={<DeleteOutlined />}
              />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ];  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '200px' }}>
        <Spin size="large" />
      </div>
    );
  }

  const activeDomains = domains.filter(d => d.status === DomainStatus.Active);
  const sslEnabledDomains = domains.filter(d => d.sslEnabled);
  const expiringSoonDomains: Domain[] = []; // No expiry tracking in current interface

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title level={2}>
          <GlobalOutlined /> Domain Management
        </Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={loadData} loading={loading}>
            Refresh
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateDomain}>
            Add Domain
          </Button>
        </Space>
      </div>

      {error && (
        <Alert
          message="API Connection Issue"
          description="Unable to connect to API. Showing sample data for demonstration."
          type="warning"
          showIcon
          style={{ marginBottom: '16px' }}
          closable
        />
      )}

      {/* Domain Statistics */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Total Domains"
              value={domains.length}
              prefix={<GlobalOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Active Domains"
              value={activeDomains.length}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#3f8600' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="SSL Certificates"
              value={sslEnabledDomains.length}
              prefix={<LockOutlined />}
              valueStyle={{ color: '#722ed1' }}
            />
            {expiringSoonDomains.length > 0 && (
              <div style={{ fontSize: '12px', color: '#fa8c16', marginTop: '4px' }}>
                {expiringSoonDomains.length} expiring soon
              </div>
            )}
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="DNS Records"
              value={Object.values(dnsStatusMap).reduce((sum, status) => sum + (status.recordCount || 0), 0)}
              prefix={<SettingOutlined />}
              valueStyle={{ color: '#13c2c2' }}
            />
            {Object.values(dnsStatusMap).filter(status => status.propagationStatus?.overallStatus === 'error').length > 0 && (
              <div style={{ fontSize: '12px', color: '#f5222d', marginTop: '4px' }}>
                {Object.values(dnsStatusMap).filter(status => status.propagationStatus?.overallStatus === 'error').length} propagation issues
              </div>
            )}
          </Card>
        </Col>
      </Row>

      {/* SSL Certificate Alerts */}
      {expiringSoonDomains.length > 0 && (
        <Alert
          message="SSL Certificate Expiry Alert"
          description={`${expiringSoonDomains.length} SSL certificate(s) will expire within 30 days. Please renew them to avoid security issues.`}
          type="warning"
          showIcon
          style={{ marginBottom: '16px' }}
          closable
        />
      )}

      <Card>
        <Table
          columns={columns}
          dataSource={domains}
          rowKey="id"
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} domains`,
          }}
        />
      </Card>

      <Modal
        title={editingDomain ? 'Edit Domain' : 'Add New Domain'}
        open={modalVisible}
        onOk={handleModalOk}
        onCancel={() => setModalVisible(false)}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            status: DomainStatus.Active,
            sslEnabled: false,
          }}
        >
          <Form.Item
            name="name"
            label="Domain Name"
            rules={[
              { required: true, message: 'Please enter domain name' },
              {
                pattern: /^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/,
                message: 'Please enter a valid domain name'
              }
            ]}
          >
            <Input placeholder="example.com" />
          </Form.Item>

          <Form.Item
            name="serverId"
            label="Server"
            rules={[{ required: true, message: 'Please select a server' }]}
          >
            <Select placeholder="Select a server">
              {servers.map(server => (
                <Select.Option key={server.id} value={server.id}>
                  {server.name} ({server.ipAddress})
                </Select.Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="documentRoot"
            label="Document Root"
            rules={[{ required: true, message: 'Please enter document root path' }]}
          >
            <Input placeholder="/var/www/example.com" />
          </Form.Item>

          <Form.Item
            name="status"
            label="Status"
            rules={[{ required: true, message: 'Please select status' }]}
          >
            <Select placeholder="Select status">
              <Select.Option value={DomainStatus.Active}>Active</Select.Option>
              <Select.Option value={DomainStatus.Inactive}>Inactive</Select.Option>
              <Select.Option value={DomainStatus.Suspended}>Suspended</Select.Option>
              <Select.Option value={DomainStatus.Expired}>Expired</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="sslEnabled"
            label="Enable SSL"
            valuePropName="checked"
          >
            <input type="checkbox" />
          </Form.Item>
        </Form>
      </Modal>

      {/* DNS Records Modal */}
      <Modal
        title={`DNS Records for ${selectedDomain?.name || ''}`}
        open={dnsModalVisible}
        onCancel={() => setDnsModalVisible(false)}
        width={1000}
        footer={[
          <Button key="close" onClick={() => setDnsModalVisible(false)}>
            Close
          </Button>,
        ]}
      >
        <div style={{ marginBottom: '16px' }}>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleCreateDnsRecord}
          >
            Add DNS Record
          </Button>
        </div>

        <Tabs defaultActiveKey="1" style={{ marginBottom: '16px' }}>
          <Tabs.TabPane tab="DNS Records" key="1">
            <Table
              columns={[
                {
                  title: 'Type',
                  dataIndex: 'type',
                  key: 'type',
                  render: (type: DnsRecordType) => (
                    <Tag color="blue">{DnsRecordType[type]}</Tag>
                  ),
                  filters: Object.values(DnsRecordType).map(type => ({
                    text: DnsRecordType[type],
                    value: type,
                  })),
                  onFilter: (value, record) => record.type === value,
                },
                {
                  title: 'Name',
                  dataIndex: 'name',
                  key: 'name',
                },
                {
                  title: 'Value',
                  dataIndex: 'value',
                  key: 'value',
                },
                {
                  title: 'TTL',
                  dataIndex: 'ttl',
                  key: 'ttl',
                  render: (ttl: number) => `${ttl}s`,
                },
                {
                  title: 'Priority',
                  dataIndex: 'priority',
                  key: 'priority',
                  render: (priority: number | null) => priority || '-',
                },
                {
                  title: 'Status',
                  dataIndex: 'status',
                  key: 'status',
                  render: (status: DnsRecordStatus) => (
                    <Tag color={status === DnsRecordStatus.Active ? 'green' : 'orange'}>
                      {DnsRecordStatus[status]}
                    </Tag>
                  ),
                },
                {
                  title: 'Actions',
                  key: 'actions',
                  render: (_, record: DnsRecord) => (
                    <Space size="small">
                      <Tooltip title="Edit Record">
                        <Button
                          type="text"
                          icon={<EditOutlined />}
                          onClick={() => handleEditDnsRecord(record)}
                        />
                      </Tooltip>
                      <Popconfirm
                        title="Delete DNS Record"
                        description="Are you sure you want to delete this DNS record?"
                        onConfirm={() => handleDeleteDnsRecord(record.id)}
                        okText="Yes"
                        cancelText="No"
                      >
                        <Tooltip title="Delete Record">
                          <Button
                            type="text"
                            danger
                            icon={<DeleteOutlined />}
                          />
                        </Tooltip>
                      </Popconfirm>
                    </Space>
                  ),
                },
              ]}
              dataSource={dnsRecords}
              rowKey="id"
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} records`,
              }}
            />
          </Tabs.TabPane>
          <Tabs.TabPane tab="Import/Export" key="2">
            <p>Import/Export functionality coming soon.</p>
          </Tabs.TabPane>
          <Tabs.TabPane tab="Zone File" key="3">
            <div style={{ marginBottom: 16 }}>
              <Button
                type="primary"
                onClick={async () => {
                  if (selectedDomain) {
                    await loadZoneFile(selectedDomain.id);
                  }
                }}
                loading={zoneFileLoading}
                style={{ marginRight: 8 }}
              >
                Load Zone File
              </Button>
              <Button
                onClick={async () => {
                  if (selectedDomain && zoneFile !== null) {
                    try {
                      await domainApi.updateDnsZone(selectedDomain.id, { zoneFile });
                      message.success('Zone file updated successfully');
                    } catch (err) {
                      console.error('Failed to update zone file:', err);
                      message.error('Failed to update zone file');
                    }
                  }
                }}
                disabled={zoneFile === null}
              >
                Save Zone File
              </Button>
            </div>
            <Input.TextArea
              value={zoneFile || ''}
              onChange={(e: React.ChangeEvent<HTMLTextAreaElement>) => setZoneFile(e.target.value)}
              placeholder="Zone file content will appear here..."
              rows={20}
              style={{ fontFamily: 'monospace' }}
            />
          </Tabs.TabPane>
          <Tabs.TabPane tab="Propagation Monitoring" key="4">
            <div style={{ marginBottom: 16 }}>
              <Space>
                <Button
                  type="primary"
                  onClick={() => selectedDomain && loadPropagationStatus(selectedDomain.id)}
                  loading={propagationLoading}
                  icon={<ReloadOutlined />}
                >
                  Refresh Status
                </Button>
                <Button
                  onClick={() => selectedDomain && checkPropagation(selectedDomain.id)}
                  loading={propagationLoading}
                  icon={<CheckCircleOutlined />}
                >
                  Check Propagation
                </Button>
              </Space>
            </div>

            {propagationStatus ? (
              <div>
                <div style={{ marginBottom: 16 }}>
                  <Card size="small">
                    <Space direction="vertical" size="small">
                      <div>
                        <Typography.Text strong>Domain:</Typography.Text> {propagationStatus.domainName}
                      </div>
                      <div>
                        <Typography.Text strong>Overall Status:</Typography.Text>{' '}
                        <Tag color={
                          propagationStatus.overallStatus === 'propagated' ? 'green' :
                          propagationStatus.overallStatus === 'propagating' ? 'orange' : 'red'
                        }>
                          {propagationStatus.overallStatus.toUpperCase()}
                        </Tag>
                      </div>
                      <div>
                        <Typography.Text strong>Last Checked:</Typography.Text> {new Date(propagationStatus.lastChecked).toLocaleString()}
                      </div>
                      <div>
                        <Typography.Text strong>Nameservers:</Typography.Text>
                        <ul style={{ margin: 0, paddingLeft: 20 }}>
                          {propagationStatus.nameservers.map((ns, index) => (
                            <li key={index}>{ns}</li>
                          ))}
                        </ul>
                      </div>
                    </Space>
                  </Card>
                </div>

                <Table
                  columns={[
                    {
                      title: 'Type',
                      dataIndex: 'type',
                      key: 'type',
                      render: (type: DnsRecordType) => (
                        <Tag color="blue">{DnsRecordType[type]}</Tag>
                      ),
                    },
                    {
                      title: 'Name',
                      dataIndex: 'name',
                      key: 'name',
                    },
                    {
                      title: 'Expected Value',
                      dataIndex: 'expectedValue',
                      key: 'expectedValue',
                    },
                    {
                      title: 'Actual Values',
                      dataIndex: 'actualValues',
                      key: 'actualValues',
                      render: (values: string[]) => (
                        <div>
                          {values.length > 0 ? (
                            <ul style={{ margin: 0, paddingLeft: 20 }}>
                              {values.map((value, index) => (
                                <li key={index}>{value}</li>
                              ))}
                            </ul>
                          ) : (
                            <Typography.Text type="secondary">No values found</Typography.Text>
                          )}
                        </div>
                      ),
                    },
                    {
                      title: 'Status',
                      dataIndex: 'isPropagated',
                      key: 'isPropagated',
                      render: (isPropagated: boolean) => (
                        <Tag color={isPropagated ? 'green' : 'orange'}>
                          {isPropagated ? 'Propagated' : 'Propagating'}
                        </Tag>
                      ),
                    },
                    {
                      title: 'Last Checked',
                      dataIndex: 'lastChecked',
                      key: 'lastChecked',
                      render: (lastChecked: string) => new Date(lastChecked).toLocaleString(),
                    },
                  ]}
                  dataSource={propagationStatus.records}
                  rowKey={(record, index) => `${record.type}-${record.name}-${index}`}
                  pagination={false}
                  size="small"
                />
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '40px' }}>
                <Typography.Text type="secondary">No propagation data available. Click "Refresh Status" to load current propagation information.</Typography.Text>
              </div>
            )}
          </Tabs.TabPane>
        </Tabs>

        {/* DNS Record Create/Edit Modal */}
        <Modal
          title={editingDnsRecord ? 'Edit DNS Record' : 'Add DNS Record'}
          open={!!editingDnsRecord || dnsForm.isFieldsTouched()}
          onOk={handleDnsModalOk}
          onCancel={() => {
            setEditingDnsRecord(null);
            dnsForm.resetFields();
          }}
          width={600}
        >
          <Form
            form={dnsForm}
            layout="vertical"
            initialValues={{
              ttl: 3600,
              priority: null,
            }}
          >
            <Form.Item
              name="type"
              label="Record Type"
              rules={[{ required: true, message: 'Please select record type' }]}
            >
              <Select placeholder="Select record type">
                <Select.Option value={DnsRecordType.A}>A (Address)</Select.Option>
                <Select.Option value={DnsRecordType.AAAA}>AAAA (IPv6 Address)</Select.Option>
                <Select.Option value={DnsRecordType.CNAME}>CNAME (Canonical Name)</Select.Option>
                <Select.Option value={DnsRecordType.MX}>MX (Mail Exchange)</Select.Option>
                <Select.Option value={DnsRecordType.TXT}>TXT (Text)</Select.Option>
                <Select.Option value={DnsRecordType.SRV}>SRV (Service)</Select.Option>
                <Select.Option value={DnsRecordType.PTR}>PTR (Pointer)</Select.Option>
                <Select.Option value={DnsRecordType.NS}>NS (Name Server)</Select.Option>
              </Select>
            </Form.Item>

            <Form.Item
              name="name"
              label="Name"
              rules={[{ required: true, message: 'Please enter record name' }]}
            >
              <Input placeholder="subdomain or @ for root" />
            </Form.Item>

            <Form.Item
              name="value"
              label="Value"
              rules={[{ required: true, message: 'Please enter record value' }]}
            >
              <Input placeholder="IP address, domain name, or text value" />
            </Form.Item>

            <Form.Item
              name="ttl"
              label="TTL (Time to Live)"
              rules={[{ required: true, message: 'Please enter TTL' }]}
            >
              <InputNumber
                min={60}
                max={86400}
                placeholder="3600"
                style={{ width: '100%' }}
              />
            </Form.Item>

            <Form.Item
              name="priority"
              label="Priority (for MX/SRV records)"
            >
              <InputNumber
                min={0}
                max={65535}
                placeholder="Optional"
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Form>
        </Modal>
      </Modal>
    </div>
  );
};

export default Domains;