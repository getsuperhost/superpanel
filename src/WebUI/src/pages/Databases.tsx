import React, { useState, useEffect } from 'react';
import {
  Card,
  Typography,
  Table,
  Button,
  Modal,
  Form,
  Input,
  Select,
  Tag,
  Space,
  Popconfirm,
  message,
  Alert,
  Spin,
  Row,
  Col,
  Statistic,
  Tooltip,
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  DatabaseOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  SettingOutlined,
  HddOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { databaseApi, serverApi, Database, Server, DatabaseStatus, ServerStatus } from '../services/api';

const { Title } = Typography;
const { Option } = Select;

const Databases: React.FC = () => {
  const [databases, setDatabases] = useState<Database[]>([]);
  const [servers, setServers] = useState<Server[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingDatabase, setEditingDatabase] = useState<Database | null>(null);
  const [form] = Form.useForm();
  const [error, setError] = useState<string | null>(null);

  // Load databases and servers on component mount
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      // Load databases and servers in parallel
      const [databasesData, serversData] = await Promise.all([
        databaseApi.getAll(),
        serverApi.getAll()
      ]);

      setDatabases(databasesData);
      setServers(serversData);
    } catch (err) {
      console.error('Failed to load data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load databases');

      // Fallback to mock data
      setDatabases([
        {
          id: 1,
          name: 'wordpress_db',
          type: 'MySQL',
          username: 'wp_user',
          sizeInMB: 256.5,
          serverId: 1,
          serverName: 'Web Server 01',
          status: DatabaseStatus.Active,
          createdAt: '2024-01-15T10:30:00Z',
          backupDate: '2024-01-20T02:00:00Z',
        },
        {
          id: 2,
          name: 'ecommerce_db',
          type: 'PostgreSQL',
          username: 'ecom_user',
          sizeInMB: 512.8,
          serverId: 1,
          serverName: 'Web Server 01',
          status: DatabaseStatus.Active,
          createdAt: '2024-02-01T14:20:00Z',
          backupDate: '2024-02-05T02:00:00Z',
        },
        {
          id: 3,
          name: 'old_backup',
          type: 'MySQL',
          username: 'backup_user',
          sizeInMB: 128.3,
          serverId: 2,
          serverName: 'Database Server',
          status: DatabaseStatus.Inactive,
          createdAt: '2023-12-01T09:15:00Z',
          backupDate: '2024-01-10T02:00:00Z',
        },
      ]);

      setServers([
        {
          id: 1,
          name: 'Web Server 01',
          ipAddress: '192.168.1.10',
          port: 80,
          status: ServerStatus.Running,
          operatingSystem: 'Ubuntu 22.04',
          totalMemoryMB: 8192,
          availableMemoryMB: 5400,
          cpuUsagePercent: 25.3,
          diskUsagePercent: 42.1,
          createdAt: '2024-01-15T10:30:00Z',
          lastUpdated: new Date().toISOString(),
        },
        {
          id: 2,
          name: 'Database Server',
          ipAddress: '192.168.1.20',
          port: 3306,
          status: ServerStatus.Running,
          operatingSystem: 'CentOS 8',
          totalMemoryMB: 16384,
          availableMemoryMB: 12800,
          cpuUsagePercent: 15.7,
          diskUsagePercent: 67.8,
          createdAt: '2024-01-20T14:20:00Z',
          lastUpdated: new Date().toISOString(),
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateDatabase = () => {
    setEditingDatabase(null);
    form.resetFields();
    setModalVisible(true);
  };

  const handleEditDatabase = (database: Database) => {
    setEditingDatabase(database);
    form.setFieldsValue({
      name: database.name,
      type: database.type,
      username: database.username,
      sizeInMB: database.sizeInMB,
      serverId: database.serverId,
      status: database.status,
    });
    setModalVisible(true);
  };

  const handleDeleteDatabase = async (id: number) => {
    try {
      await databaseApi.delete(id);
      message.success('Database deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete database');
      console.error('Delete database error:', err);
    }
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();

      if (editingDatabase) {
        // Update existing database
        await databaseApi.update(editingDatabase.id, values);
        message.success('Database updated successfully');
      } else {
        // Create new database
        await databaseApi.create(values);
        message.success('Database created successfully');
      }

      setModalVisible(false);
      loadData();
    } catch (err) {
      if (err instanceof Error && err.message.includes('API Error')) {
        message.error('Failed to save database - API not available');
      } else {
        message.error('Please check all required fields');
      }
      console.error('Save database error:', err);
    }
  };

  const getStatusColor = (status: DatabaseStatus): string => {
    switch (status) {
      case DatabaseStatus.Active:
        return 'green';
      case DatabaseStatus.Inactive:
        return 'orange';
      case DatabaseStatus.Suspended:
        return 'red';
      case DatabaseStatus.Corrupted:
        return 'red';
      default:
        return 'default';
    }
  };

  const getStatusText = (status: DatabaseStatus): string => {
    switch (status) {
      case DatabaseStatus.Active:
        return 'Active';
      case DatabaseStatus.Inactive:
        return 'Inactive';
      case DatabaseStatus.Suspended:
        return 'Suspended';
      case DatabaseStatus.Corrupted:
        return 'Corrupted';
      default:
        return 'Unknown';
    }
  };

  const getServerName = (serverId: number) => {
    const server = servers.find(s => s.id === serverId);
    return server ? server.name : `Server ${serverId}`;
  };

  const formatSize = (sizeInMB: number): string => {
    if (sizeInMB >= 1024) {
      return `${(sizeInMB / 1024).toFixed(1)} GB`;
    }
    return `${sizeInMB.toFixed(1)} MB`;
  };

  const columns: ColumnsType<Database> = [
    {
      title: 'Database Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string) => (
        <Space>
          <DatabaseOutlined style={{ color: '#1890ff' }} />
          <span style={{ fontWeight: 'bold' }}>{name}</span>
        </Space>
      ),
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      render: (type: string) => (
        <Tag color={type === 'MySQL' ? 'blue' : type === 'PostgreSQL' ? 'green' : 'purple'}>
          {type}
        </Tag>
      ),
      filters: [
        { text: 'MySQL', value: 'MySQL' },
        { text: 'PostgreSQL', value: 'PostgreSQL' },
        { text: 'SQL Server', value: 'SQL Server' },
        { text: 'MongoDB', value: 'MongoDB' },
      ],
      onFilter: (value, record) => record.type === value,
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
      title: 'Size',
      dataIndex: 'sizeInMB',
      key: 'sizeInMB',
      render: (sizeInMB: number) => (
        <Space>
          <HddOutlined />
          {formatSize(sizeInMB)}
        </Space>
      ),
      sorter: (a, b) => a.sizeInMB - b.sizeInMB,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: DatabaseStatus) => (
        <Tag color={getStatusColor(status)}>
          {getStatusText(status)}
        </Tag>
      ),
      filters: [
        { text: 'Active', value: DatabaseStatus.Active },
        { text: 'Inactive', value: DatabaseStatus.Inactive },
        { text: 'Suspended', value: DatabaseStatus.Suspended },
        { text: 'Corrupted', value: DatabaseStatus.Corrupted },
      ],
      onFilter: (value, record) => record.status === value,
    },
    {
      title: 'Username',
      dataIndex: 'username',
      key: 'username',
      responsive: ['md'],
    },
    {
      title: 'Last Backup',
      dataIndex: 'backupDate',
      key: 'backupDate',
      render: (date: string | null) => date ? new Date(date).toLocaleDateString() : 'Never',
      sorter: (a, b) => {
        if (!a.backupDate && !b.backupDate) return 0;
        if (!a.backupDate) return 1;
        if (!b.backupDate) return -1;
        return new Date(a.backupDate).getTime() - new Date(b.backupDate).getTime();
      },
      responsive: ['lg'],
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleDateString(),
      sorter: (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
      responsive: ['lg'],
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: Database) => (
        <Space size="small">
          <Tooltip title="Edit Database">
            <Button
              type="text"
              icon={<EditOutlined />}
              onClick={() => handleEditDatabase(record)}
            />
          </Tooltip>
          <Tooltip title="Database Settings">
            <Button
              type="text"
              icon={<SettingOutlined />}
              onClick={() => message.info('Database settings coming soon')}
            />
          </Tooltip>
          <Popconfirm
            title="Delete Database"
            description="Are you sure you want to delete this database? This action cannot be undone."
            onConfirm={() => handleDeleteDatabase(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Tooltip title="Delete Database">
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
  ];

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '200px' }}>
        <Spin size="large" />
      </div>
    );
  }

  const activeDatabases = databases.filter(db => db.status === DatabaseStatus.Active);
  const totalSize = databases.reduce((sum, db) => sum + db.sizeInMB, 0);
  const databasesWithBackups = databases.filter(db => db.backupDate);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title level={2}>
          <DatabaseOutlined /> Database Management
        </Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={loadData} loading={loading}>
            Refresh
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateDatabase}>
            Add Database
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

      {/* Database Statistics */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Total Databases"
              value={databases.length}
              prefix={<DatabaseOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Active Databases"
              value={activeDatabases.length}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#3f8600' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Total Size"
              value={formatSize(totalSize)}
              prefix={<HddOutlined />}
              valueStyle={{ color: '#722ed1' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Backed Up"
              value={databasesWithBackups.length}
              suffix={`/ ${databases.length}`}
              prefix={<DatabaseOutlined />}
              valueStyle={{ color: '#fa8c16' }}
            />
          </Card>
        </Col>
      </Row>

      <Card>
        <Table
          columns={columns}
          dataSource={databases}
          rowKey="id"
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} databases`,
          }}
        />
      </Card>

      <Modal
        title={editingDatabase ? 'Edit Database' : 'Add New Database'}
        open={modalVisible}
        onOk={handleModalOk}
        onCancel={() => setModalVisible(false)}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            type: 'MySQL',
            status: DatabaseStatus.Active,
          }}
        >
          <Form.Item
            name="name"
            label="Database Name"
            rules={[
              { required: true, message: 'Please enter database name' },
              {
                pattern: /^[a-zA-Z_][a-zA-Z0-9_]*$/,
                message: 'Database name must start with letter or underscore and contain only alphanumeric characters and underscores'
              }
            ]}
          >
            <Input placeholder="my_database" />
          </Form.Item>

          <Form.Item
            name="type"
            label="Database Type"
            rules={[{ required: true, message: 'Please select database type' }]}
          >
            <Select placeholder="Select database type">
              <Option value="MySQL">MySQL</Option>
              <Option value="PostgreSQL">PostgreSQL</Option>
              <Option value="SQL Server">SQL Server</Option>
              <Option value="MongoDB">MongoDB</Option>
              <Option value="SQLite">SQLite</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="serverId"
            label="Server"
            rules={[{ required: true, message: 'Please select a server' }]}
          >
            <Select placeholder="Select a server">
              {servers.map(server => (
                <Option key={server.id} value={server.id}>
                  {server.name} ({server.ipAddress})
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="username"
            label="Database User"
            rules={[{ required: true, message: 'Please enter database username' }]}
          >
            <Input placeholder="db_user" />
          </Form.Item>

          <Form.Item
            name="sizeInMB"
            label="Size (MB)"
            rules={[
              { required: true, message: 'Please enter database size' },
              { type: 'number', min: 0, message: 'Size must be a positive number' }
            ]}
          >
            <Input type="number" placeholder="256" />
          </Form.Item>

          <Form.Item
            name="status"
            label="Status"
            rules={[{ required: true, message: 'Please select status' }]}
          >
            <Select placeholder="Select status">
              <Option value={DatabaseStatus.Active}>Active</Option>
              <Option value={DatabaseStatus.Inactive}>Inactive</Option>
              <Option value={DatabaseStatus.Suspended}>Suspended</Option>
              <Option value={DatabaseStatus.Corrupted}>Corrupted</Option>
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default Databases;