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
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  PlayCircleOutlined,
  PauseCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { serverApi, healthApi, Server, ServerStatus } from '../services/api';

const { Title } = Typography;
const { Option } = Select;

const Servers: React.FC = () => {
  const [servers, setServers] = useState<Server[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingServer, setEditingServer] = useState<Server | null>(null);
  const [form] = Form.useForm();
  const [error, setError] = useState<string | null>(null);

  // Load servers on component mount
  useEffect(() => {
    loadServers();
  }, []);

  const loadServers = async () => {
    try {
      setLoading(true);
      setError(null);
      
      // Try to get data from health API first, then fall back to original API
      let serverList;
      try {
        const mockServers = await healthApi.getMockServers();
        serverList = mockServers.map(s => ({
          id: s.id,
          name: s.name,
          ipAddress: s.ipAddress,
          port: s.port,
          status: s.status as ServerStatus,
          operatingSystem: s.operatingSystem,
          totalMemoryMB: s.totalMemoryMB,
          availableMemoryMB: s.availableMemoryMB,
          cpuUsagePercent: s.cpuUsagePercent,
          diskUsagePercent: s.diskUsagePercent,
          createdAt: s.createdAt,
          lastUpdated: s.lastUpdated,
        }));
      } catch {
        // Fall back to original API
        serverList = await serverApi.getAll();
      }
      
      setServers(serverList);
    } catch (err) {
      console.error('Failed to load servers:', err);
      setError(err instanceof Error ? err.message : 'Failed to load servers');
      
      // Ultimate fallback to hardcoded mock data
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
        {
          id: 3,
          name: 'Backup Server',
          ipAddress: '192.168.1.30',
          port: 22,
          status: ServerStatus.Stopped,
          operatingSystem: 'Debian 11',
          totalMemoryMB: 4096,
          availableMemoryMB: 3200,
          cpuUsagePercent: 5.2,
          diskUsagePercent: 89.3,
          createdAt: '2024-02-01T09:15:00Z',
          lastUpdated: new Date().toISOString(),
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateServer = () => {
    setEditingServer(null);
    form.resetFields();
    setModalVisible(true);
  };

  const handleEditServer = (server: Server) => {
    setEditingServer(server);
    form.setFieldsValue(server);
    setModalVisible(true);
  };

  const handleDeleteServer = async (id: number) => {
    try {
      await serverApi.delete(id);
      message.success('Server deleted successfully');
      loadServers();
    } catch (err) {
      message.error('Failed to delete server');
      console.error('Delete server error:', err);
    }
  };

  const handleStatusChange = async (id: number, newStatus: ServerStatus) => {
    try {
      await serverApi.updateStatus(id, newStatus);
      message.success(`Server status updated to ${newStatus}`);
      loadServers();
    } catch (err) {
      message.error('Failed to update server status');
      console.error('Update status error:', err);
    }
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();
      
      if (editingServer) {
        // Update existing server
        await serverApi.update(editingServer.id, values);
        message.success('Server updated successfully');
      } else {
        // Create new server
        await serverApi.create(values);
        message.success('Server created successfully');
      }
      
      setModalVisible(false);
      loadServers();
    } catch (err) {
      if (err instanceof Error && err.message.includes('API Error')) {
        message.error('Failed to save server - API not available');
      } else {
        message.error('Please check all required fields');
      }
      console.error('Save server error:', err);
    }
  };

  const getStatusColor = (status: ServerStatus): string => {
    switch (status) {
      case ServerStatus.Running:
        return 'green';
      case ServerStatus.Stopped:
        return 'red';
      case ServerStatus.Error:
        return 'volcano';
      case ServerStatus.Maintenance:
        return 'orange';
      default:
        return 'default';
    }
  };

  const columns: ColumnsType<Server> = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: 'IP Address',
      dataIndex: 'ipAddress',
      key: 'ipAddress',
    },
    {
      title: 'Port',
      dataIndex: 'port',
      key: 'port',
      width: 80,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: ServerStatus) => (
        <Tag color={getStatusColor(status)}>{status}</Tag>
      ),
      filters: [
        { text: 'Running', value: ServerStatus.Running },
        { text: 'Stopped', value: ServerStatus.Stopped },
        { text: 'Error', value: ServerStatus.Error },
        { text: 'Maintenance', value: ServerStatus.Maintenance },
      ],
      onFilter: (value, record) => record.status === value,
    },
    {
      title: 'OS',
      dataIndex: 'operatingSystem',
      key: 'operatingSystem',
      responsive: ['md'],
    },
    {
      title: 'CPU %',
      dataIndex: 'cpuUsagePercent',
      key: 'cpuUsagePercent',
      render: (cpu: number) => `${cpu.toFixed(1)}%`,
      sorter: (a, b) => a.cpuUsagePercent - b.cpuUsagePercent,
      responsive: ['lg'],
    },
    {
      title: 'Memory',
      key: 'memory',
      render: (_, record: Server) => {
        const usedMB = record.totalMemoryMB - record.availableMemoryMB;
        const usagePercent = (usedMB / record.totalMemoryMB) * 100;
        return `${usagePercent.toFixed(1)}%`;
      },
      sorter: (a, b) => {
        const aUsage = (a.totalMemoryMB - a.availableMemoryMB) / a.totalMemoryMB;
        const bUsage = (b.totalMemoryMB - b.availableMemoryMB) / b.totalMemoryMB;
        return aUsage - bUsage;
      },
      responsive: ['lg'],
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: Server) => (
        <Space size="small">
          <Button
            type="text"
            icon={record.status === ServerStatus.Running ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
            onClick={() => handleStatusChange(
              record.id,
              record.status === ServerStatus.Running ? ServerStatus.Stopped : ServerStatus.Running
            )}
            title={record.status === ServerStatus.Running ? 'Stop Server' : 'Start Server'}
          />
          <Button
            type="text"
            icon={<EditOutlined />}
            onClick={() => handleEditServer(record)}
            title="Edit Server"
          />
          <Popconfirm
            title="Delete Server"
            description="Are you sure you want to delete this server?"
            onConfirm={() => handleDeleteServer(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              title="Delete Server"
            />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title level={2} style={{ margin: 0 }}>Server Management</Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={loadServers} loading={loading}>
            Refresh
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateServer}>
            Add Server
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

      <Card>
        <Table
          columns={columns}
          dataSource={servers}
          rowKey="id"
          loading={loading}
          pagination={{
            pageSize: 10,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} servers`,
          }}
        />
      </Card>

      <Modal
        title={editingServer ? 'Edit Server' : 'Add New Server'}
        open={modalVisible}
        onOk={handleModalOk}
        onCancel={() => setModalVisible(false)}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            status: ServerStatus.Stopped,
            port: 80,
          }}
        >
          <Form.Item
            name="name"
            label="Server Name"
            rules={[{ required: true, message: 'Please enter server name' }]}
          >
            <Input placeholder="Enter server name" />
          </Form.Item>

          <Form.Item
            name="ipAddress"
            label="IP Address"
            rules={[
              { required: true, message: 'Please enter IP address' },
              { pattern: /^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$/, message: 'Please enter valid IP address' },
            ]}
          >
            <Input placeholder="192.168.1.100" />
          </Form.Item>

          <Form.Item
            name="port"
            label="Port"
            rules={[
              { required: true, message: 'Please enter port number' },
              { type: 'number', min: 1, max: 65535, message: 'Port must be between 1 and 65535' },
            ]}
          >
            <Input type="number" placeholder="80" />
          </Form.Item>

          <Form.Item
            name="operatingSystem"
            label="Operating System"
            rules={[{ required: true, message: 'Please enter operating system' }]}
          >
            <Input placeholder="Ubuntu 22.04" />
          </Form.Item>

          <Form.Item
            name="status"
            label="Status"
            rules={[{ required: true, message: 'Please select status' }]}
          >
            <Select>
              <Option value={ServerStatus.Running}>Running</Option>
              <Option value={ServerStatus.Stopped}>Stopped</Option>
              <Option value={ServerStatus.Maintenance}>Maintenance</Option>
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default Servers;