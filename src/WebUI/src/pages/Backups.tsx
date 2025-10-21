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
  Tabs,
  Alert,
  Tooltip,
  Dropdown,
  Switch,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  PlusOutlined,
  DownloadOutlined,
  ReloadOutlined,
  DeleteOutlined,
  PlayCircleOutlined,
  PauseCircleOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  MoreOutlined,
  FileTextOutlined,
  DatabaseOutlined,
  GlobalOutlined,
  MailOutlined,
  SaveOutlined,
  EditOutlined,
} from '@ant-design/icons';
import {
  backupApi,
  backupScheduleApi,
  serverApi,
  databaseApi,
  domainApi,
  Server,
  Database,
  Domain
} from '../services/api';
import {
  Backup,
  BackupLog,
  BackupSchedule,
  BackupType,
  BackupStatus
} from '../types/backup';

const { Title } = Typography;
const { Option } = Select;
const { TabPane } = Tabs;
const { TextArea } = Input;

const Backups: React.FC = () => {
  const [backups, setBackups] = useState<Backup[]>([]);
  const [backupSchedules, setBackupSchedules] = useState<BackupSchedule[]>([]);
  const [servers, setServers] = useState<Server[]>([]);
  const [databases, setDatabases] = useState<Database[]>([]);
  const [domains, setDomains] = useState<Domain[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalVisible, setModalVisible] = useState(false);
  const [scheduleModalVisible, setScheduleModalVisible] = useState(false);
  const [restoreModalVisible, setRestoreModalVisible] = useState(false);
  const [logsModalVisible, setLogsModalVisible] = useState(false);
  const [editingBackup, setEditingBackup] = useState<Backup | null>(null);
  const [editingSchedule, setEditingSchedule] = useState<BackupSchedule | null>(null);
  const [selectedBackup, setSelectedBackup] = useState<Backup | null>(null);
  const [backupLogs, setBackupLogs] = useState<BackupLog[]>([]);
  const [form] = Form.useForm();
  const [scheduleForm] = Form.useForm();
  const [restoreForm] = Form.useForm();
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('backups');

  // Load data on component mount
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      const [
        backupsData,
        schedulesData,
        serversData,
        databasesData,
        domainsData
      ] = await Promise.all([
        backupApi.getAll().catch(() => []),
        backupScheduleApi.getAll().catch(() => []),
        serverApi.getAll().catch(() => []),
        databaseApi.getAll().catch(() => []),
        domainApi.getAll().catch(() => []),
      ]);

      setBackups(backupsData);
      setBackupSchedules(schedulesData);
      setServers(serversData);
      setDatabases(databasesData);
      setDomains(domainsData);
    } catch (err) {
      console.error('Failed to load backup data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load backup data');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateBackup = () => {
    setEditingBackup(null);
    form.resetFields();
    setModalVisible(true);
  };

  const handleCreateSchedule = () => {
    setEditingSchedule(null);
    scheduleForm.resetFields();
    setScheduleModalVisible(true);
  };

  const handleEditSchedule = (schedule: BackupSchedule) => {
    setEditingSchedule(schedule);
    scheduleForm.setFieldsValue(schedule);
    setScheduleModalVisible(true);
  };

  const handleDeleteBackup = async (id: number) => {
    try {
      await backupApi.delete(id);
      message.success('Backup deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete backup');
      console.error('Delete backup error:', err);
    }
  };

  const handleDeleteSchedule = async (id: number) => {
    try {
      await backupScheduleApi.delete(id);
      message.success('Backup schedule deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete backup schedule');
      console.error('Delete schedule error:', err);
    }
  };

  const handleRestoreBackup = (backup: Backup) => {
    setSelectedBackup(backup);
    restoreForm.resetFields();
    setRestoreModalVisible(true);
  };

  const handleDownloadBackup = async (backup: Backup) => {
    try {
      const blob = await backupApi.download(backup.id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${backup.name}_${backup.id}.zip`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      message.success('Backup download started');
    } catch (err) {
      message.error('Failed to download backup');
      console.error('Download error:', err);
    }
  };

  const handleViewLogs = async (backup: Backup) => {
    try {
      const logs = await backupApi.getLogs(backup.id);
      setBackupLogs(logs);
      setSelectedBackup(backup);
      setLogsModalVisible(true);
    } catch (err) {
      message.error('Failed to load backup logs');
      console.error('Load logs error:', err);
    }
  };

  const handleCancelBackup = async (id: number) => {
    try {
      await backupApi.cancel(id);
      message.success('Backup cancellation requested');
      loadData();
    } catch (err) {
      message.error('Failed to cancel backup');
      console.error('Cancel backup error:', err);
    }
  };

  const handleBackupModalOk = async () => {
    try {
      const values = await form.validateFields();

      if (editingBackup) {
        // Update existing backup (if supported)
        message.info('Backup update not implemented yet');
      } else {
        // Create new backup
        await backupApi.create(values);
        message.success('Backup created successfully');
      }

      setModalVisible(false);
      loadData();
    } catch (err) {
      message.error('Failed to save backup');
      console.error('Save backup error:', err);
    }
  };

  const handleScheduleModalOk = async () => {
    try {
      const values = await scheduleForm.validateFields();

      if (editingSchedule) {
        // Update existing schedule
        await backupScheduleApi.update(editingSchedule.id, values);
        message.success('Backup schedule updated successfully');
      } else {
        // Create new schedule
        await backupScheduleApi.create(values);
        message.success('Backup schedule created successfully');
      }

      setScheduleModalVisible(false);
      loadData();
    } catch (err) {
      message.error('Failed to save backup schedule');
      console.error('Save schedule error:', err);
    }
  };

  const handleRestoreModalOk = async () => {
    if (!selectedBackup) return;

    try {
      const values = await restoreForm.validateFields();
      const result = await backupApi.restore(selectedBackup.id, values);

      if (result.success) {
        message.success(`Restore completed: ${result.message}`);
      } else {
        message.error(`Restore failed: ${result.message}`);
      }

      setRestoreModalVisible(false);
      loadData();
    } catch (err) {
      message.error('Failed to restore backup');
      console.error('Restore error:', err);
    }
  };

  const handleToggleSchedule = async (schedule: BackupSchedule) => {
    try {
      if (schedule.isActive) {
        await backupScheduleApi.disable(schedule.id);
        message.success('Backup schedule disabled');
      } else {
        await backupScheduleApi.enable(schedule.id);
        message.success('Backup schedule enabled');
      }
      loadData();
    } catch (err) {
      message.error('Failed to toggle schedule status');
      console.error('Toggle schedule error:', err);
    }
  };

  const getStatusColor = (status: BackupStatus): string => {
    switch (status) {
      case BackupStatus.Completed:
        return 'green';
      case BackupStatus.InProgress:
        return 'blue';
      case BackupStatus.Pending:
        return 'orange';
      case BackupStatus.Failed:
        return 'red';
      case BackupStatus.Cancelled:
        return 'gray';
      default:
        return 'default';
    }
  };

  const getStatusIcon = (status: BackupStatus) => {
    switch (status) {
      case BackupStatus.Completed:
        return <CheckCircleOutlined />;
      case BackupStatus.InProgress:
        return <ClockCircleOutlined />;
      case BackupStatus.Pending:
        return <ExclamationCircleOutlined />;
      case BackupStatus.Failed:
        return <CloseCircleOutlined />;
      case BackupStatus.Cancelled:
        return <PauseCircleOutlined />;
      default:
        return null;
    }
  };

  const getTypeIcon = (type: BackupType) => {
    switch (type) {
      case BackupType.Database:
        return <DatabaseOutlined />;
      case BackupType.Files:
        return <FileTextOutlined />;
      case BackupType.FullServer:
        return <SaveOutlined />;
      case BackupType.Website:
        return <GlobalOutlined />;
      case BackupType.Email:
        return <MailOutlined />;
      default:
        return <FileTextOutlined />;
    }
  };

  const formatFileSize = (bytes?: number): string => {
    if (!bytes) return 'N/A';
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(2)} ${sizes[i]}`;
  };

  const backupColumns: ColumnsType<Backup> = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record: Backup) => (
        <Space>
          {getTypeIcon(record.type)}
          {name}
        </Space>
      ),
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      render: (type: BackupType) => (
        <Tag color="blue">{type}</Tag>
      ),
      filters: Object.values(BackupType).map(type => ({ text: type, value: type })),
      onFilter: (value, record) => record.type === value,
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: BackupStatus) => (
        <Tag color={getStatusColor(status)} icon={getStatusIcon(status)}>
          {status}
        </Tag>
      ),
      filters: Object.values(BackupStatus).map(status => ({ text: status, value: status })),
      onFilter: (value, record) => record.status === value,
    },
    {
      title: 'Size',
      dataIndex: 'fileSizeInBytes',
      key: 'fileSizeInBytes',
      render: (size: number) => formatFileSize(size),
      sorter: (a, b) => (a.fileSizeInBytes || 0) - (b.fileSizeInBytes || 0),
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleString(),
      sorter: (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: Backup) => {
        const menuItems = [
          {
            key: 'download',
            icon: <DownloadOutlined />,
            label: 'Download',
            onClick: () => handleDownloadBackup(record),
            disabled: record.status !== BackupStatus.Completed,
          },
          {
            key: 'restore',
            icon: <ReloadOutlined />,
            label: 'Restore',
            onClick: () => handleRestoreBackup(record),
            disabled: record.status !== BackupStatus.Completed,
          },
          {
            key: 'logs',
            icon: <FileTextOutlined />,
            label: 'View Logs',
            onClick: () => handleViewLogs(record),
          },
          {
            key: 'cancel',
            icon: <PauseCircleOutlined />,
            label: 'Cancel',
            onClick: () => handleCancelBackup(record.id),
            disabled: record.status !== BackupStatus.InProgress && record.status !== BackupStatus.Pending,
          },
        ];

        return (
          <Space size="small">
            <Dropdown menu={{ items: menuItems }} trigger={['click']}>
              <Button type="text" icon={<MoreOutlined />} />
            </Dropdown>
            <Popconfirm
              title="Delete Backup"
              description="Are you sure you want to delete this backup?"
              onConfirm={() => handleDeleteBackup(record.id)}
              okText="Yes"
              cancelText="No"
            >
              <Button
                type="text"
                danger
                icon={<DeleteOutlined />}
                title="Delete Backup"
              />
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  const scheduleColumns: ColumnsType<BackupSchedule> = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record: BackupSchedule) => (
        <Space>
          {getTypeIcon(record.backupType)}
          {name}
        </Space>
      ),
      sorter: (a, b) => a.name.localeCompare(b.name),
    },
    {
      title: 'Type',
      dataIndex: 'backupType',
      key: 'backupType',
      render: (type: BackupType) => (
        <Tag color="blue">{type}</Tag>
      ),
    },
    {
      title: 'Schedule',
      dataIndex: 'scheduleExpression',
      key: 'scheduleExpression',
      render: (expression: string) => (
        <Tooltip title={`Cron: ${expression}`}>
          <ClockCircleOutlined /> {expression}
        </Tooltip>
      ),
    },
    {
      title: 'Status',
      dataIndex: 'isActive',
      key: 'isActive',
      render: (isActive: boolean) => (
        <Tag color={isActive ? 'green' : 'red'}>
          {isActive ? 'Active' : 'Inactive'}
        </Tag>
      ),
    },
    {
      title: 'Next Run',
      dataIndex: 'nextRunAt',
      key: 'nextRunAt',
      render: (date?: string) => date ? new Date(date).toLocaleString() : 'N/A',
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: BackupSchedule) => (
        <Space size="small">
          <Button
            type="text"
            icon={record.isActive ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
            onClick={() => handleToggleSchedule(record)}
            title={record.isActive ? 'Disable Schedule' : 'Enable Schedule'}
          />
          <Button
            type="text"
            icon={<EditOutlined />}
            onClick={() => handleEditSchedule(record)}
            title="Edit Schedule"
          />
          <Popconfirm
            title="Delete Schedule"
            description="Are you sure you want to delete this backup schedule?"
            onConfirm={() => handleDeleteSchedule(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              title="Delete Schedule"
            />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title level={2} style={{ margin: 0 }}>Backup Management</Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={loadData} loading={loading}>
            Refresh
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateBackup}>
            Create Backup
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

      <Tabs activeKey={activeTab} onChange={setActiveTab}>
        <TabPane tab="Backups" key="backups">
          <Card>
            <Table
              columns={backupColumns}
              dataSource={backups}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} backups`,
              }}
            />
          </Card>
        </TabPane>

        <TabPane tab="Schedules" key="schedules">
          <div style={{ marginBottom: '16px' }}>
            <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateSchedule}>
              Create Schedule
            </Button>
          </div>
          <Card>
            <Table
              columns={scheduleColumns}
              dataSource={backupSchedules}
              rowKey="id"
              loading={loading}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} schedules`,
              }}
            />
          </Card>
        </TabPane>
      </Tabs>

      {/* Create/Edit Backup Modal */}
      <Modal
        title={editingBackup ? 'Edit Backup' : 'Create New Backup'}
        open={modalVisible}
        onOk={handleBackupModalOk}
        onCancel={() => setModalVisible(false)}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            isCompressed: true,
            isEncrypted: false,
            retentionDays: 30,
          }}
        >
          <Form.Item
            name="name"
            label="Backup Name"
            rules={[{ required: true, message: 'Please enter backup name' }]}
          >
            <Input placeholder="Enter backup name" />
          </Form.Item>

          <Form.Item
            name="description"
            label="Description"
          >
            <TextArea placeholder="Enter backup description" rows={3} />
          </Form.Item>

          <Form.Item
            name="type"
            label="Backup Type"
            rules={[{ required: true, message: 'Please select backup type' }]}
          >
            <Select placeholder="Select backup type">
              <Option value={BackupType.Database}>Database</Option>
              <Option value={BackupType.Files}>Files</Option>
              <Option value={BackupType.FullServer}>Full Server</Option>
              <Option value={BackupType.Website}>Website</Option>
              <Option value={BackupType.Email}>Email</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="serverId"
            label="Server"
          >
            <Select placeholder="Select server (optional)">
              {servers.map(server => (
                <Option key={server.id} value={server.id}>{server.name}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="databaseId"
            label="Database"
          >
            <Select placeholder="Select database (optional)">
              {databases.map(db => (
                <Option key={db.id} value={db.id}>{db.name}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="domainId"
            label="Domain"
          >
            <Select placeholder="Select domain (optional)">
              {domains.map(domain => (
                <Option key={domain.id} value={domain.id}>{domain.domainName}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="backupPath"
            label="Backup Path"
          >
            <Input placeholder="Enter backup path (optional)" />
          </Form.Item>

          <Form.Item
            name="isCompressed"
            label="Compress Backup"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="isEncrypted"
            label="Encrypt Backup"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="retentionDays"
            label="Retention Days"
            rules={[{ required: true, message: 'Please enter retention days' }]}
          >
            <Input type="number" placeholder="30" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Create/Edit Schedule Modal */}
      <Modal
        title={editingSchedule ? 'Edit Backup Schedule' : 'Create New Backup Schedule'}
        open={scheduleModalVisible}
        onOk={handleScheduleModalOk}
        onCancel={() => setScheduleModalVisible(false)}
        width={600}
      >
        <Form
          form={scheduleForm}
          layout="vertical"
          initialValues={{
            isCompressed: true,
            isEncrypted: false,
            retentionDays: 30,
            isActive: true,
            scheduleExpression: '0 2 * * *', // Daily at 2 AM
          }}
        >
          <Form.Item
            name="name"
            label="Schedule Name"
            rules={[{ required: true, message: 'Please enter schedule name' }]}
          >
            <Input placeholder="Enter schedule name" />
          </Form.Item>

          <Form.Item
            name="description"
            label="Description"
          >
            <TextArea placeholder="Enter schedule description" rows={3} />
          </Form.Item>

          <Form.Item
            name="backupType"
            label="Backup Type"
            rules={[{ required: true, message: 'Please select backup type' }]}
          >
            <Select placeholder="Select backup type">
              <Option value={BackupType.Database}>Database</Option>
              <Option value={BackupType.Files}>Files</Option>
              <Option value={BackupType.FullServer}>Full Server</Option>
              <Option value={BackupType.Website}>Website</Option>
              <Option value={BackupType.Email}>Email</Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="scheduleExpression"
            label="Schedule (Cron Expression)"
            rules={[{ required: true, message: 'Please enter cron expression' }]}
          >
            <Input placeholder="0 2 * * * (daily at 2 AM)" />
          </Form.Item>

          <Form.Item
            name="serverId"
            label="Server"
          >
            <Select placeholder="Select server (optional)">
              {servers.map(server => (
                <Option key={server.id} value={server.id}>{server.name}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="databaseId"
            label="Database"
          >
            <Select placeholder="Select database (optional)">
              {databases.map(db => (
                <Option key={db.id} value={db.id}>{db.name}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="domainId"
            label="Domain"
          >
            <Select placeholder="Select domain (optional)">
              {domains.map(domain => (
                <Option key={domain.id} value={domain.id}>{domain.domainName}</Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="backupPath"
            label="Backup Path"
          >
            <Input placeholder="Enter backup path (optional)" />
          </Form.Item>

          <Form.Item
            name="isCompressed"
            label="Compress Backup"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="isEncrypted"
            label="Encrypt Backup"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="retentionDays"
            label="Retention Days"
            rules={[{ required: true, message: 'Please enter retention days' }]}
          >
            <Input type="number" placeholder="30" />
          </Form.Item>

          <Form.Item
            name="isActive"
            label="Active"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      {/* Restore Modal */}
      <Modal
        title={`Restore Backup: ${selectedBackup?.name}`}
        open={restoreModalVisible}
        onOk={handleRestoreModalOk}
        onCancel={() => setRestoreModalVisible(false)}
        width={500}
      >
        <Form
          form={restoreForm}
          layout="vertical"
          initialValues={{
            overwriteExisting: false,
          }}
        >
          <Alert
            message="Warning"
            description="Restoring a backup will overwrite existing data. Make sure you have a backup of your current data."
            type="warning"
            showIcon
            style={{ marginBottom: '16px' }}
          />

          <Form.Item
            name="restorePath"
            label="Restore Path"
          >
            <Input placeholder="Enter restore path (optional)" />
          </Form.Item>

          <Form.Item
            name="overwriteExisting"
            label="Overwrite Existing Files"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      {/* Logs Modal */}
      <Modal
        title={`Backup Logs: ${selectedBackup?.name}`}
        open={logsModalVisible}
        onCancel={() => setLogsModalVisible(false)}
        footer={null}
        width={800}
      >
        <Table
          columns={[
            {
              title: 'Timestamp',
              dataIndex: 'timestamp',
              key: 'timestamp',
              render: (timestamp: string) => new Date(timestamp).toLocaleString(),
              width: 180,
            },
            {
              title: 'Level',
              dataIndex: 'level',
              key: 'level',
              render: (level: string) => (
                <Tag color={level === 'Error' ? 'red' : level === 'Warning' ? 'orange' : 'blue'}>
                  {level}
                </Tag>
              ),
              width: 80,
            },
            {
              title: 'Message',
              dataIndex: 'message',
              key: 'message',
            },
          ]}
          dataSource={backupLogs}
          rowKey="id"
          pagination={{
            pageSize: 20,
            showSizeChanger: true,
          }}
          scroll={{ y: 400 }}
        />
      </Modal>
    </div>
  );
};

export default Backups;