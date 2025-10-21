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
  MailOutlined,
  ForwardOutlined,
  LinkOutlined,
  CheckCircleOutlined,
  ReloadOutlined,
  UserOutlined,
} from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { emailApi, domainApi, EmailAccount, EmailForwarder, EmailAlias, Domain } from '../services/api';

const { Title } = Typography;
const { TabPane } = Tabs;

const Emails: React.FC = () => {
  const [emailAccounts, setEmailAccounts] = useState<EmailAccount[]>([]);
  const [domains, setDomains] = useState<Domain[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingAccount, setEditingAccount] = useState<EmailAccount | null>(null);
  const [form] = Form.useForm();
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('accounts');

  // Forwarder and Alias management
  const [forwarderModalVisible, setForwarderModalVisible] = useState(false);
  const [aliasModalVisible, setAliasModalVisible] = useState(false);
  const [selectedAccountId, setSelectedAccountId] = useState<number | null>(null);
  const [forwarderForm] = Form.useForm();
  const [aliasForm] = Form.useForm();

  // Load email accounts and domains on component mount
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);

      // Load email accounts and domains in parallel
      const [emailData, domainsData] = await Promise.all([
        emailApi.getAll(),
        domainApi.getAll()
      ]);

      setEmailAccounts(emailData);
      setDomains(domainsData);
    } catch (err) {
      console.error('Failed to load data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load email accounts');

      // Fallback to mock data
      setEmailAccounts([
        {
          id: 1,
          emailAddress: 'admin@example.com',
          domainId: 1,
          domainName: 'example.com',
          passwordHash: 'hashed_password',
          quotaMB: 1024,
          usedQuotaMB: 256,
          isActive: true,
          createdAt: '2024-01-15T10:30:00Z',
          lastLoginAt: '2024-01-20T14:30:00Z',
          forwarders: [],
          aliases: [],
        },
        {
          id: 2,
          emailAddress: 'support@example.com',
          domainId: 1,
          domainName: 'example.com',
          passwordHash: 'hashed_password',
          quotaMB: 512,
          usedQuotaMB: 128,
          isActive: true,
          createdAt: '2024-02-01T14:20:00Z',
          lastLoginAt: '2024-02-05T09:15:00Z',
          forwarders: [
            {
              id: 1,
              emailAccountId: 2,
              forwardTo: 'admin@example.com',
              createdAt: '2024-02-01T14:20:00Z',
            }
          ],
          aliases: [
            {
              id: 1,
              emailAccountId: 2,
              aliasAddress: 'help@example.com',
              createdAt: '2024-02-01T14:20:00Z',
            }
          ],
        },
        {
          id: 3,
          emailAddress: 'old@example.com',
          domainId: 2,
          domainName: 'old-domain.com',
          passwordHash: 'hashed_password',
          quotaMB: 256,
          usedQuotaMB: 0,
          isActive: false,
          createdAt: '2023-12-01T09:15:00Z',
          lastLoginAt: null,
          forwarders: [],
          aliases: [],
        },
      ]);

      setDomains([
        {
          id: 1,
          domainName: 'example.com',
          serverId: 1,
          serverName: 'Web Server 01',
          documentRoot: '/var/www/example.com',
          isActive: true,
          sslEnabled: true,
          createdAt: '2024-01-15T10:30:00Z',
          lastUpdated: '2024-01-15T10:30:00Z',
        },
        {
          id: 2,
          domainName: 'old-domain.com',
          serverId: 2,
          serverName: 'Database Server',
          documentRoot: '/var/www/old-domain.com',
          isActive: false,
          sslEnabled: true,
          createdAt: '2023-12-01T09:15:00Z',
          lastUpdated: '2024-01-10T16:45:00Z',
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateAccount = () => {
    setEditingAccount(null);
    form.resetFields();
    setModalVisible(true);
  };

  const handleEditAccount = (account: EmailAccount) => {
    setEditingAccount(account);
    form.setFieldsValue({
      emailAddress: account.emailAddress.split('@')[0], // Extract username part
      domainId: account.domainId,
      password: '', // Don't show existing password
      quotaMB: account.quotaMB,
      isActive: account.isActive,
    });
    setModalVisible(true);
  };

  const handleDeleteAccount = async (id: number) => {
    try {
      await emailApi.delete(id);
      message.success('Email account deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete email account');
      console.error('Delete email account error:', err);
    }
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();

      const accountData = {
        emailAddress: `${values.emailAddress}@${domains.find(d => d.id === values.domainId)?.domainName}`,
        domainId: values.domainId,
        password: values.password,
        quotaMB: values.quotaMB,
        isActive: values.isActive,
      };

      if (editingAccount) {
        // Update existing account
        const updateData = {
          password: values.password || undefined,
          quotaMB: values.quotaMB,
          isActive: values.isActive,
        };
        await emailApi.update(editingAccount.id, updateData);
        message.success('Email account updated successfully');
      } else {
        // Create new account
        await emailApi.create(accountData);
        message.success('Email account created successfully');
      }

      setModalVisible(false);
      loadData();
    } catch (err) {
      if (err instanceof Error && err.message.includes('API Error')) {
        message.error('Failed to save email account - API not available');
      } else {
        message.error('Please check all required fields');
      }
      console.error('Save email account error:', err);
    }
  };

  // Forwarder management
  const handleAddForwarder = (accountId: number) => {
    setSelectedAccountId(accountId);
    forwarderForm.resetFields();
    setForwarderModalVisible(true);
  };

  const handleForwarderModalOk = async () => {
    try {
      const values = await forwarderForm.validateFields();
      await emailApi.addForwarder(selectedAccountId!, values);
      message.success('Forwarder added successfully');
      setForwarderModalVisible(false);
      loadData();
    } catch (err) {
      message.error('Failed to add forwarder');
      console.error('Add forwarder error:', err);
    }
  };

  const handleDeleteForwarder = async (forwarderId: number) => {
    try {
      await emailApi.deleteForwarder(forwarderId);
      message.success('Forwarder deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete forwarder');
      console.error('Delete forwarder error:', err);
    }
  };

  // Alias management
  const handleAddAlias = (accountId: number) => {
    setSelectedAccountId(accountId);
    aliasForm.resetFields();
    setAliasModalVisible(true);
  };

  const handleAliasModalOk = async () => {
    try {
      const values = await aliasForm.validateFields();
      await emailApi.addAlias(selectedAccountId!, values);
      message.success('Alias added successfully');
      setAliasModalVisible(false);
      loadData();
    } catch (err) {
      message.error('Failed to add alias');
      console.error('Add alias error:', err);
    }
  };

  const handleDeleteAlias = async (aliasId: number) => {
    try {
      await emailApi.deleteAlias(aliasId);
      message.success('Alias deleted successfully');
      loadData();
    } catch (err) {
      message.error('Failed to delete alias');
      console.error('Delete alias error:', err);
    }
  };

  const getDomainName = (domainId: number) => {
    const domain = domains.find(d => d.id === domainId);
    return domain ? domain.domainName : `Domain ${domainId}`;
  };

  const getQuotaUsage = (account: EmailAccount) => {
    const usagePercent = (account.usedQuotaMB / account.quotaMB) * 100;
    if (usagePercent >= 90) return { status: 'error' as const, color: 'red' };
    if (usagePercent >= 75) return { status: 'warning' as const, color: 'orange' };
    return { status: 'success' as const, color: 'green' };
  };

  const accountColumns: ColumnsType<EmailAccount> = [
    {
      title: 'Email Address',
      dataIndex: 'emailAddress',
      key: 'emailAddress',
      render: (emailAddress: string) => (
        <Space>
          <MailOutlined style={{ color: '#1890ff' }} />
          <span>{emailAddress}</span>
        </Space>
      ),
      sorter: (a, b) => a.emailAddress.localeCompare(b.emailAddress),
    },
    {
      title: 'Domain',
      dataIndex: 'domainId',
      key: 'domainId',
      render: (domainId: number) => getDomainName(domainId),
      filters: domains.map(domain => ({ text: domain.domainName, value: domain.id })),
      onFilter: (value, record) => record.domainId === value,
    },
    {
      title: 'Status',
      dataIndex: 'isActive',
      key: 'isActive',
      render: (isActive: boolean) => (
        <Tag color={isActive ? 'green' : 'red'}>{isActive ? 'Active' : 'Inactive'}</Tag>
      ),
      filters: [
        { text: 'Active', value: true },
        { text: 'Inactive', value: false },
      ],
      onFilter: (value, record) => record.isActive === value,
    },
    {
      title: 'Quota Usage',
      key: 'quota',
      render: (_, record: EmailAccount) => {
        const quotaStatus = getQuotaUsage(record);
        const usagePercent = Math.round((record.usedQuotaMB / record.quotaMB) * 100);
        return (
          <Tooltip title={`${record.usedQuotaMB}MB / ${record.quotaMB}MB (${usagePercent}%)`}>
            <Badge
              status={quotaStatus.status}
              text={`${usagePercent}%`}
            />
          </Tooltip>
        );
      },
      sorter: (a, b) => (a.usedQuotaMB / a.quotaMB) - (b.usedQuotaMB / b.quotaMB),
    },
    {
      title: 'Forwarders',
      key: 'forwarders',
      render: (_, record: EmailAccount) => (
        <Badge count={record.forwarders?.length || 0} showZero>
          <ForwardOutlined style={{ color: '#52c41a' }} />
        </Badge>
      ),
    },
    {
      title: 'Aliases',
      key: 'aliases',
      render: (_, record: EmailAccount) => (
        <Badge count={record.aliases?.length || 0} showZero>
          <LinkOutlined style={{ color: '#722ed1' }} />
        </Badge>
      ),
    },
    {
      title: 'Last Login',
      dataIndex: 'lastLoginAt',
      key: 'lastLoginAt',
      render: (date: string | null) => date ? new Date(date).toLocaleDateString() : 'Never',
      sorter: (a, b) => {
        if (!a.lastLoginAt && !b.lastLoginAt) return 0;
        if (!a.lastLoginAt) return 1;
        if (!b.lastLoginAt) return -1;
        return new Date(a.lastLoginAt).getTime() - new Date(b.lastLoginAt).getTime();
      },
      responsive: ['md'],
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: EmailAccount) => (
        <Space size="small">
          <Tooltip title="Edit Account">
            <Button
              type="text"
              icon={<EditOutlined />}
              onClick={() => handleEditAccount(record)}
            />
          </Tooltip>
          <Tooltip title="Add Forwarder">
            <Button
              type="text"
              icon={<ForwardOutlined />}
              onClick={() => handleAddForwarder(record.id)}
            />
          </Tooltip>
          <Tooltip title="Add Alias">
            <Button
              type="text"
              icon={<LinkOutlined />}
              onClick={() => handleAddAlias(record.id)}
            />
          </Tooltip>
          <Popconfirm
            title="Delete Email Account"
            description="Are you sure you want to delete this email account? This will also delete all associated forwarders and aliases."
            onConfirm={() => handleDeleteAccount(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Tooltip title="Delete Account">
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

  const forwarderColumns: ColumnsType<EmailForwarder> = [
    {
      title: 'Forward To',
      dataIndex: 'forwardTo',
      key: 'forwardTo',
      render: (forwardTo: string) => (
        <Space>
          <ForwardOutlined style={{ color: '#52c41a' }} />
          <span>{forwardTo}</span>
        </Space>
      ),
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleDateString(),
      sorter: (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: EmailForwarder) => (
        <Popconfirm
          title="Delete Forwarder"
          description="Are you sure you want to delete this forwarder?"
          onConfirm={() => handleDeleteForwarder(record.id)}
          okText="Yes"
          cancelText="No"
        >
          <Button type="text" danger icon={<DeleteOutlined />} />
        </Popconfirm>
      ),
    },
  ];

  const aliasColumns: ColumnsType<EmailAlias> = [
    {
      title: 'Alias Address',
      dataIndex: 'aliasAddress',
      key: 'aliasAddress',
      render: (aliasAddress: string) => (
        <Space>
          <LinkOutlined style={{ color: '#722ed1' }} />
          <span>{aliasAddress}</span>
        </Space>
      ),
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleDateString(),
      sorter: (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime(),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record: EmailAlias) => (
        <Popconfirm
          title="Delete Alias"
          description="Are you sure you want to delete this alias?"
          onConfirm={() => handleDeleteAlias(record.id)}
          okText="Yes"
          cancelText="No"
        >
          <Button type="text" danger icon={<DeleteOutlined />} />
        </Popconfirm>
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

  const activeAccounts = emailAccounts.filter(a => a.isActive);
  const totalForwarders = emailAccounts.reduce((sum, account) => sum + (account.forwarders?.length || 0), 0);
  const totalAliases = emailAccounts.reduce((sum, account) => sum + (account.aliases?.length || 0), 0);
  const accountsNearQuota = emailAccounts.filter(account => {
    const usagePercent = (account.usedQuotaMB / account.quotaMB) * 100;
    return usagePercent >= 80;
  });

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title level={2}>
          <MailOutlined /> Email Management
        </Title>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={loadData} loading={loading}>
            Refresh
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleCreateAccount}>
            Add Email Account
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

      {/* Email Statistics */}
      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Total Accounts"
              value={emailAccounts.length}
              prefix={<MailOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Active Accounts"
              value={activeAccounts.length}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#3f8600' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Forwarders"
              value={totalForwarders}
              prefix={<ForwardOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={6}>
          <Card>
            <Statistic
              title="Aliases"
              value={totalAliases}
              prefix={<LinkOutlined />}
              valueStyle={{ color: '#722ed1' }}
            />
          </Card>
        </Col>
      </Row>

      {/* Quota Alert */}
      {accountsNearQuota.length > 0 && (
        <Alert
          message="Quota Alert"
          description={`${accountsNearQuota.length} email account(s) are using 80% or more of their quota. Consider increasing quotas or cleaning up old emails.`}
          type="warning"
          showIcon
          style={{ marginBottom: '16px' }}
          closable
        />
      )}

      <Card>
        <Tabs activeKey={activeTab} onChange={setActiveTab}>
          <TabPane
            tab={
              <span>
                <MailOutlined />
                Email Accounts ({emailAccounts.length})
              </span>
            }
            key="accounts"
          >
            <Table
              columns={accountColumns}
              dataSource={emailAccounts}
              rowKey="id"
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} email accounts`,
              }}
            />
          </TabPane>

          <TabPane
            tab={
              <span>
                <ForwardOutlined />
                All Forwarders ({totalForwarders})
              </span>
            }
            key="forwarders"
          >
            {emailAccounts.flatMap(account =>
              (account.forwarders || []).map(forwarder => ({
                ...forwarder,
                accountEmail: account.emailAddress,
              }))
            ).length > 0 ? (
              <Table
                columns={[
                  {
                    title: 'Account',
                    dataIndex: 'accountEmail',
                    key: 'accountEmail',
                    render: (email: string) => (
                      <Space>
                        <UserOutlined />
                        <span>{email}</span>
                      </Space>
                    ),
                  },
                  ...forwarderColumns,
                ]}
                dataSource={emailAccounts.flatMap(account =>
                  (account.forwarders || []).map(forwarder => ({
                    ...forwarder,
                    accountEmail: account.emailAddress,
                  }))
                )}
                rowKey="id"
                pagination={{
                  pageSize: 10,
                  showSizeChanger: true,
                  showQuickJumper: true,
                  showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} forwarders`,
                }}
              />
            ) : (
              <div style={{ textAlign: 'center', padding: '40px' }}>
                <ForwardOutlined style={{ fontSize: '48px', color: '#d9d9d9' }} />
                <div style={{ marginTop: '16px', color: '#8c8c8c' }}>
                  No forwarders configured yet
                </div>
              </div>
            )}
          </TabPane>

          <TabPane
            tab={
              <span>
                <LinkOutlined />
                All Aliases ({totalAliases})
              </span>
            }
            key="aliases"
          >
            {emailAccounts.flatMap(account =>
              (account.aliases || []).map(alias => ({
                ...alias,
                accountEmail: account.emailAddress,
              }))
            ).length > 0 ? (
              <Table
                columns={[
                  {
                    title: 'Account',
                    dataIndex: 'accountEmail',
                    key: 'accountEmail',
                    render: (email: string) => (
                      <Space>
                        <UserOutlined />
                        <span>{email}</span>
                      </Space>
                    ),
                  },
                  ...aliasColumns,
                ]}
                dataSource={emailAccounts.flatMap(account =>
                  (account.aliases || []).map(alias => ({
                    ...alias,
                    accountEmail: account.emailAddress,
                  }))
                )}
                rowKey="id"
                pagination={{
                  pageSize: 10,
                  showSizeChanger: true,
                  showQuickJumper: true,
                  showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} aliases`,
                }}
              />
            ) : (
              <div style={{ textAlign: 'center', padding: '40px' }}>
                <LinkOutlined style={{ fontSize: '48px', color: '#d9d9d9' }} />
                <div style={{ marginTop: '16px', color: '#8c8c8c' }}>
                  No aliases configured yet
                </div>
              </div>
            )}
          </TabPane>
        </Tabs>
      </Card>

      {/* Email Account Modal */}
      <Modal
        title={editingAccount ? 'Edit Email Account' : 'Add New Email Account'}
        open={modalVisible}
        onOk={handleModalOk}
        onCancel={() => setModalVisible(false)}
        width={600}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            isActive: true,
            quotaMB: 1024,
          }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="emailAddress"
                label="Username"
                rules={[
                  { required: true, message: 'Please enter username' },
                  { pattern: /^[a-zA-Z0-9._-]+$/, message: 'Username can only contain letters, numbers, dots, underscores, and hyphens' }
                ]}
              >
                <Input placeholder="admin" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="domainId"
                label="Domain"
                rules={[{ required: true, message: 'Please select a domain' }]}
              >
                <Select placeholder="Select domain">
                  {domains.filter(d => d.isActive).map(domain => (
                    <Select.Option key={domain.id} value={domain.id}>
                      {domain.domainName}
                    </Select.Option>
                  ))}
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="password"
            label={editingAccount ? "New Password (leave empty to keep current)" : "Password"}
            rules={[
              { required: !editingAccount, message: 'Please enter password' },
              { min: 8, message: 'Password must be at least 8 characters' }
            ]}
          >
            <Input.Password placeholder="Enter password" />
          </Form.Item>

          <Form.Item
            name="quotaMB"
            label="Quota (MB)"
            rules={[
              { required: true, message: 'Please enter quota' },
              { type: 'number', min: 1, message: 'Quota must be at least 1 MB' }
            ]}
          >
            <Input type="number" placeholder="1024" />
          </Form.Item>

          <Form.Item
            name="isActive"
            label="Active"
            valuePropName="checked"
          >
            <input type="checkbox" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Forwarder Modal */}
      <Modal
        title="Add Email Forwarder"
        open={forwarderModalVisible}
        onOk={handleForwarderModalOk}
        onCancel={() => setForwarderModalVisible(false)}
        width={500}
      >
        <Form form={forwarderForm} layout="vertical">
          <Form.Item
            name="forwardTo"
            label="Forward To"
            rules={[
              { required: true, message: 'Please enter forward-to email' },
              { type: 'email', message: 'Please enter a valid email address' }
            ]}
          >
            <Input placeholder="destination@example.com" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Alias Modal */}
      <Modal
        title="Add Email Alias"
        open={aliasModalVisible}
        onOk={handleAliasModalOk}
        onCancel={() => setAliasModalVisible(false)}
        width={500}
      >
        <Form form={aliasForm} layout="vertical">
          <Form.Item
            name="aliasAddress"
            label="Alias Address"
            rules={[
              { required: true, message: 'Please enter alias address' },
              { type: 'email', message: 'Please enter a valid email address' }
            ]}
          >
            <Input placeholder="alias@example.com" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default Emails;