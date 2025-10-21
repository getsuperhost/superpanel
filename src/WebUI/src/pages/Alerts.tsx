import React, { useState, useEffect } from 'react';
import {
  Card,
  Table,
  Button,
  Modal,
  Form,
  Input,
  Select,
  Switch,
  Tag,
  Space,
  message,
  Statistic,
  Row,
  Col,
  Tabs,
  Tooltip,
  Popconfirm,
  Timeline,
  Avatar,
  Divider,
  Typography
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  BellOutlined,
  ExperimentOutlined,
  BarChartOutlined,
  HistoryOutlined,
  CommentOutlined,
  UserOutlined
} from '@ant-design/icons';
import {
  AlertRule,
  Alert,
  AlertStats,
  CreateAlertRuleRequest,
  UpdateAlertRuleRequest,
  AlertRuleType,
  AlertRuleSeverity,
  AlertRuleStatus,
  NotificationChannel,
  AlertHistory,
  AlertComment
} from '../types/alerts';
import { alertRulesApi, alertsApi } from '../services/api';

const { TabPane } = Tabs;
const { Option } = Select;

const Alerts: React.FC = () => {
  const [alertRules, setAlertRules] = useState<AlertRule[]>([]);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [alertStats, setAlertStats] = useState<AlertStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingRule, setEditingRule] = useState<AlertRule | null>(null);
  const [form] = Form.useForm();

  // History and Comments state
  const [selectedAlert, setSelectedAlert] = useState<Alert | null>(null);
  const [alertHistory, setAlertHistory] = useState<AlertHistory[]>([]);
  const [alertComments, setAlertComments] = useState<AlertComment[]>([]);
  const [historyModalVisible, setHistoryModalVisible] = useState(false);
  const [actionModalVisible, setActionModalVisible] = useState(false);
  const [actionType, setActionType] = useState<'acknowledge' | 'resolve'>('acknowledge');
  const [actionForm] = Form.useForm();

  // Load data on component mount
  useEffect(() => {
    loadAlertRules();
    loadAlerts();
    loadAlertStats();
  }, []);

  const loadAlertRules = async () => {
    try {
      const rules = await alertRulesApi.getAll();
      setAlertRules(rules);
    } catch {
      message.error('Failed to load alert rules');
    }
  };

  const loadAlerts = async () => {
    try {
      const alertsData = await alertsApi.getAll();
      setAlerts(alertsData);
    } catch {
      message.error('Failed to load alerts');
    }
  };

  const loadAlertStats = async () => {
    try {
      const stats = await alertsApi.getStats();
      setAlertStats(stats);
    } catch {
      message.error('Failed to load alert statistics');
    }
  };

  const handleCreateRule = async (values: CreateAlertRuleRequest) => {
    try {
      setLoading(true);
      await alertRulesApi.create(values);
      message.success('Alert rule created successfully');
      setModalVisible(false);
      form.resetFields();
      loadAlertRules();
    } catch {
      message.error('Failed to create alert rule');
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateRule = async (id: number, values: UpdateAlertRuleRequest) => {
    try {
      setLoading(true);
      await alertRulesApi.update(id, values);
      message.success('Alert rule updated successfully');
      setModalVisible(false);
      setEditingRule(null);
      form.resetFields();
      loadAlertRules();
    } catch {
      message.error('Failed to update alert rule');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteRule = async (id: number) => {
    try {
      await alertRulesApi.delete(id);
      message.success('Alert rule deleted successfully');
      loadAlertRules();
    } catch {
      message.error('Failed to delete alert rule');
    }
  };

  const handleTestRule = async (id: number) => {
    try {
      await alertRulesApi.test(id);
      message.success('Test alert created successfully');
      loadAlerts();
    } catch {
      message.error('Failed to test alert rule');
    }
  };

  const handleAcknowledgeAlert = async (alertId: number) => {
    setSelectedAlert(alerts.find(a => a.id === alertId) || null);
    setActionType('acknowledge');
    setActionModalVisible(true);
  };

  const handleResolveAlert = async (alertId: number) => {
    setSelectedAlert(alerts.find(a => a.id === alertId) || null);
    setActionType('resolve');
    setActionModalVisible(true);
  };

  const handleActionModalOk = async () => {
    if (!selectedAlert) return;

    try {
      const values = await actionForm.validateFields();
      const comment = values.comment?.trim();

      if (actionType === 'acknowledge') {
        await alertsApi.acknowledgeWithComment(selectedAlert.id, comment, 'User');
        message.success('Alert acknowledged successfully');
      } else {
        await alertsApi.resolveWithComment(selectedAlert.id, comment, 'User');
        message.success('Alert resolved successfully');
      }

      setActionModalVisible(false);
      actionForm.resetFields();
      loadAlerts();
      loadAlertStats();
    } catch {
      message.error(`Failed to ${actionType} alert`);
    }
  };

  const handleViewHistory = async (alert: Alert) => {
    setSelectedAlert(alert);
    try {
      const [history, comments] = await Promise.all([
        alertsApi.getHistory(alert.id),
        alertsApi.getComments(alert.id)
      ]);
      setAlertHistory(history);
      setAlertComments(comments);
      setHistoryModalVisible(true);
    } catch {
      message.error('Failed to load alert history');
    }
  };

  const handleAddComment = async (alertId: number, comment: string) => {
    try {
      await alertsApi.addComment(alertId, comment, 'General', 'User');
      // Reload comments
      const comments = await alertsApi.getComments(alertId);
      setAlertComments(comments);
      message.success('Comment added successfully');
    } catch {
      message.error('Failed to add comment');
    }
  };

  const handleEvaluateRules = async () => {
    try {
      await alertsApi.evaluate();
      message.success('Alert rules evaluated successfully');
      loadAlerts();
      loadAlertStats();
    } catch {
      message.error('Failed to evaluate alert rules');
    }
  };

  const openCreateModal = () => {
    setEditingRule(null);
    form.resetFields();
    setModalVisible(true);
  };

  const openEditModal = (rule: AlertRule) => {
    setEditingRule(rule);
    form.setFieldsValue({
      name: rule.name,
      description: rule.description,
      type: rule.type,
      serverId: rule.serverId,
      metricName: rule.metricName,
      condition: rule.condition,
      threshold: rule.threshold,
      severity: rule.severity,
      enabled: rule.enabled,
      cooldownMinutes: rule.cooldownMinutes,
      notificationChannels: rule.notificationChannels,
      webhookUrl: rule.webhookUrl,
      emailRecipients: rule.emailRecipients,
      slackWebhookUrl: rule.slackWebhookUrl,
    });
    setModalVisible(true);
  };

  const handleModalOk = async () => {
    try {
      const values = await form.validateFields();
      if (editingRule) {
        await handleUpdateRule(editingRule.id, values);
      } else {
        await handleCreateRule(values);
      }
    } catch {
      // Form validation error
    }
  };

  const getSeverityColor = (severity: AlertRuleSeverity) => {
    switch (severity) {
      case AlertRuleSeverity.Critical: return 'red';
      case AlertRuleSeverity.Warning: return 'orange';
      case AlertRuleSeverity.Info: return 'blue';
      default: return 'default';
    }
  };

  const getStatusColor = (status: AlertRuleStatus) => {
    switch (status) {
      case AlertRuleStatus.Active: return 'red';
      case AlertRuleStatus.Acknowledged: return 'orange';
      case AlertRuleStatus.Resolved: return 'green';
      default: return 'default';
    }
  };

  const alertRulesColumns = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      render: (type: AlertRuleType) => <Tag>{type}</Tag>,
    },
    {
      title: 'Severity',
      dataIndex: 'severity',
      key: 'severity',
      render: (severity: AlertRuleSeverity) => (
        <Tag color={getSeverityColor(severity)}>{severity}</Tag>
      ),
    },
    {
      title: 'Enabled',
      dataIndex: 'enabled',
      key: 'enabled',
      render: (enabled: boolean) => (
        <Switch checked={enabled} disabled size="small" />
      ),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_text: string, record: AlertRule) => (
        <Space>
          <Tooltip title="Edit">
            <Button
              icon={<EditOutlined />}
              onClick={() => openEditModal(record)}
              size="small"
            />
          </Tooltip>
          <Tooltip title="Test">
            <Button
              icon={<ExperimentOutlined />}
              onClick={() => handleTestRule(record.id)}
              size="small"
            />
          </Tooltip>
          <Popconfirm
            title="Delete this alert rule?"
            onConfirm={() => handleDeleteRule(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Button icon={<DeleteOutlined />} danger size="small" />
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const alertsColumns = [
    {
      title: 'Message',
      dataIndex: 'message',
      key: 'message',
      ellipsis: true,
    },
    {
      title: 'Severity',
      dataIndex: 'severity',
      key: 'severity',
      render: (severity: AlertRuleSeverity) => (
        <Tag color={getSeverityColor(severity)}>{severity}</Tag>
      ),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: AlertRuleStatus) => (
        <Tag color={getStatusColor(status)}>{status}</Tag>
      ),
    },
    {
      title: 'Server',
      dataIndex: 'serverName',
      key: 'serverName',
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleString(),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_text: string, record: Alert) => (
        <Space>
          {record.status === AlertRuleStatus.Active && (
            <Button
              icon={<CheckCircleOutlined />}
              onClick={() => handleAcknowledgeAlert(record.id)}
              size="small"
            >
              Acknowledge
            </Button>
          )}
          {record.status !== AlertRuleStatus.Resolved && (
            <Button
              icon={<ExclamationCircleOutlined />}
              onClick={() => handleResolveAlert(record.id)}
              size="small"
            >
              Resolve
            </Button>
          )}
          <Button
            icon={<HistoryOutlined />}
            onClick={() => handleViewHistory(record)}
            size="small"
          >
            History
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ padding: '24px' }}>
      <Row gutter={[16, 16]}>
        <Col span={24}>
          <Card title={<><BellOutlined /> Alert Management</>}>
            <Tabs defaultActiveKey="overview">
              <TabPane tab="Overview" key="overview">
                {alertStats && (
                  <Row gutter={16} style={{ marginBottom: 24 }}>
                    <Col span={4}>
                      <Statistic
                        title="Total Alerts"
                        value={alertStats.totalAlerts}
                        prefix={<BarChartOutlined />}
                      />
                    </Col>
                    <Col span={4}>
                      <Statistic
                        title="Active"
                        value={alertStats.activeAlerts}
                        valueStyle={{ color: '#cf1322' }}
                      />
                    </Col>
                    <Col span={4}>
                      <Statistic
                        title="Critical"
                        value={alertStats.criticalAlerts}
                        valueStyle={{ color: '#cf1322' }}
                      />
                    </Col>
                    <Col span={4}>
                      <Statistic
                        title="Warning"
                        value={alertStats.warningAlerts}
                        valueStyle={{ color: '#fa8c16' }}
                      />
                    </Col>
                    <Col span={4}>
                      <Statistic
                        title="Acknowledged"
                        value={alertStats.acknowledgedAlerts}
                        valueStyle={{ color: '#fa8c16' }}
                      />
                    </Col>
                    <Col span={4}>
                      <Button
                        type="primary"
                        icon={<ExperimentOutlined />}
                        onClick={handleEvaluateRules}
                      >
                        Evaluate Rules
                      </Button>
                    </Col>
                  </Row>
                )}
              </TabPane>

              <TabPane tab="Alert Rules" key="rules">
                <div style={{ marginBottom: 16 }}>
                  <Button
                    type="primary"
                    icon={<PlusOutlined />}
                    onClick={openCreateModal}
                  >
                    Create Alert Rule
                  </Button>
                </div>
                <Table
                  columns={alertRulesColumns}
                  dataSource={alertRules}
                  rowKey="id"
                  loading={loading}
                />
              </TabPane>

              <TabPane tab="Active Alerts" key="alerts">
                <Table
                  columns={alertsColumns}
                  dataSource={alerts.filter(a => a.status === AlertRuleStatus.Active)}
                  rowKey="id"
                  loading={loading}
                />
              </TabPane>

              <TabPane tab="All Alerts" key="all-alerts">
                <Table
                  columns={alertsColumns}
                  dataSource={alerts}
                  rowKey="id"
                  loading={loading}
                />
              </TabPane>

              <TabPane tab={<><HistoryOutlined /> History & Comments</>} key="history">
                <div style={{ padding: '20px' }}>
                  <Typography.Title level={4}>Alert History & Comments</Typography.Title>
                  <Typography.Text type="secondary">
                    Select an alert from the tables above to view its history and add comments.
                  </Typography.Text>
                  {selectedAlert && (
                    <div style={{ marginTop: '20px' }}>
                      <Card title={`Alert ID: ${selectedAlert.id}`} size="small">
                        <p><strong>Message:</strong> {selectedAlert.message}</p>
                        <p><strong>Status:</strong> <Tag color={getStatusColor(selectedAlert.status)}>{selectedAlert.status}</Tag></p>
                        <p><strong>Severity:</strong> <Tag color={getSeverityColor(selectedAlert.severity)}>{selectedAlert.severity}</Tag></p>
                      </Card>

                      <Divider />

                      <Row gutter={16}>
                        <Col span={12}>
                          <Card title="History Timeline" size="small">
                            <Timeline>
                              {alertHistory.map((item) => (
                                <Timeline.Item
                                  key={item.id}
                                  color={item.action === 'Created' ? 'blue' : item.action === 'Resolved' ? 'green' : 'orange'}
                                >
                                  <div>
                                    <strong>{item.action}</strong>
                                    <br />
                                    <small>{new Date(item.timestamp).toLocaleString()}</small>
                                    {item.description && (
                                      <div style={{ marginTop: '4px', fontSize: '12px', color: '#666' }}>
                                        {item.description}
                                      </div>
                                    )}
                                  </div>
                                </Timeline.Item>
                              ))}
                            </Timeline>
                          </Card>
                        </Col>
                        <Col span={12}>
                          <Card title="Comments" size="small">
                            <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                              {alertComments.map((comment) => (
                                <div key={comment.id} style={{ marginBottom: '12px', padding: '8px', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
                                  <div style={{ display: 'flex', alignItems: 'center', marginBottom: '4px' }}>
                                    <Avatar size="small" icon={<UserOutlined />} />
                                    <span style={{ marginLeft: '8px', fontWeight: 'bold' }}>{comment.createdBy}</span>
                                    <span style={{ marginLeft: '8px', fontSize: '12px', color: '#666' }}>
                                      {new Date(comment.createdAt).toLocaleString()}
                                    </span>
                                  </div>
                                  <div>{comment.comment}</div>
                                  {comment.commentType !== 'General' && (
                                    <Tag style={{ marginTop: '4px' }}>{comment.commentType}</Tag>
                                  )}
                                </div>
                              ))}
                            </div>
                            <Divider />
                            <Form layout="inline">
                              <Form.Item style={{ flex: 1 }}>
                                <Input.TextArea
                                  placeholder="Add a comment..."
                                  rows={2}
                                  onPressEnter={(e) => {
                                    e.preventDefault();
                                    const value = e.currentTarget.value.trim();
                                    if (value && selectedAlert) {
                                      handleAddComment(selectedAlert.id, value);
                                      e.currentTarget.value = '';
                                    }
                                  }}
                                />
                              </Form.Item>
                              <Form.Item>
                                <Button
                                  type="primary"
                                  icon={<CommentOutlined />}
                                  onClick={() => {
                                    const textarea = document.querySelector('textarea[placeholder="Add a comment..."]') as HTMLTextAreaElement;
                                    const value = textarea?.value.trim();
                                    if (value && selectedAlert) {
                                      handleAddComment(selectedAlert.id, value);
                                      textarea.value = '';
                                    }
                                  }}
                                >
                                  Add
                                </Button>
                              </Form.Item>
                            </Form>
                          </Card>
                        </Col>
                      </Row>
                    </div>
                  )}
                </div>
              </TabPane>
            </Tabs>
          </Card>
        </Col>
      </Row>

      {/* Alert Rule Modal */}
      <Modal
        title={editingRule ? 'Edit Alert Rule' : 'Create Alert Rule'}
        open={modalVisible}
        onOk={handleModalOk}
        onCancel={() => {
          setModalVisible(false);
          setEditingRule(null);
          form.resetFields();
        }}
        width={800}
        confirmLoading={loading}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            condition: 'gt',
            severity: AlertRuleSeverity.Warning,
            enabled: true,
            cooldownMinutes: 5,
            notificationChannels: [NotificationChannel.Email],
          }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="name"
                label="Name"
                rules={[{ required: true, message: 'Please enter a name' }]}
              >
                <Input placeholder="Alert rule name" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="type"
                label="Type"
                rules={[{ required: true, message: 'Please select a type' }]}
              >
                <Select placeholder="Select alert type">
                  {Object.values(AlertRuleType).map(type => (
                    <Option key={type} value={type}>{type}</Option>
                  ))}
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="description" label="Description">
            <Input.TextArea placeholder="Optional description" rows={2} />
          </Form.Item>

          <Row gutter={16}>
            <Col span={8}>
              <Form.Item
                name="condition"
                label="Condition"
                rules={[{ required: true, message: 'Please select a condition' }]}
              >
                <Select>
                  <Option value="gt">Greater than</Option>
                  <Option value="lt">Less than</Option>
                  <Option value="eq">Equal to</Option>
                  <Option value="ne">Not equal to</Option>
                </Select>
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="threshold"
                label="Threshold"
                rules={[{ required: true, message: 'Please enter a threshold' }]}
              >
                <Input type="number" placeholder="Threshold value" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="severity"
                label="Severity"
                rules={[{ required: true, message: 'Please select severity' }]}
              >
                <Select>
                  {Object.values(AlertRuleSeverity).map(severity => (
                    <Option key={severity} value={severity}>{severity}</Option>
                  ))}
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={8}>
              <Form.Item name="enabled" label="Enabled" valuePropName="checked">
                <Switch />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="cooldownMinutes"
                label="Cooldown (minutes)"
                rules={[{ required: true, message: 'Please enter cooldown minutes' }]}
              >
                <Input type="number" min={1} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="notificationChannels"
                label="Notification Channels"
                rules={[{ required: true, message: 'Please select notification channels' }]}
              >
                <Select mode="multiple" placeholder="Select channels">
                  {Object.values(NotificationChannel).map(channel => (
                    <Option key={channel} value={channel}>{channel}</Option>
                  ))}
                </Select>
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="webhookUrl" label="Webhook URL">
            <Input placeholder="https://example.com/webhook" />
          </Form.Item>

          <Form.Item name="emailRecipients" label="Email Recipients">
            <Select mode="tags" placeholder="Enter email addresses" />
          </Form.Item>

          <Form.Item name="slackWebhookUrl" label="Slack Webhook URL">
            <Input placeholder="https://hooks.slack.com/..." />
          </Form.Item>
        </Form>
      </Modal>

      {/* Action Modal (Acknowledge/Resolve with Comment) */}
      <Modal
        title={`${actionType === 'acknowledge' ? 'Acknowledge' : 'Resolve'} Alert`}
        open={actionModalVisible}
        onOk={handleActionModalOk}
        onCancel={() => {
          setActionModalVisible(false);
          actionForm.resetFields();
        }}
        confirmLoading={loading}
      >
        {selectedAlert && (
          <div style={{ marginBottom: '16px' }}>
            <p><strong>Alert:</strong> {selectedAlert.message}</p>
            <p><strong>Current Status:</strong> <Tag color={getStatusColor(selectedAlert.status)}>{selectedAlert.status}</Tag></p>
          </div>
        )}
        <Form form={actionForm} layout="vertical">
          <Form.Item
            name="comment"
            label={`Comment (optional) - Why are you ${actionType === 'acknowledge' ? 'acknowledging' : 'resolving'} this alert?`}
          >
            <Input.TextArea
              placeholder={`Add a comment about ${actionType === 'acknowledge' ? 'acknowledging' : 'resolving'} this alert...`}
              rows={3}
            />
          </Form.Item>
        </Form>
      </Modal>

      {/* History Modal */}
      <Modal
        title="Alert History & Comments"
        open={historyModalVisible}
        onCancel={() => setHistoryModalVisible(false)}
        width={1000}
        footer={null}
      >
        {selectedAlert && (
          <div>
            <Card title={`Alert ID: ${selectedAlert.id}`} size="small" style={{ marginBottom: '16px' }}>
              <p><strong>Message:</strong> {selectedAlert.message}</p>
              <p><strong>Status:</strong> <Tag color={getStatusColor(selectedAlert.status)}>{selectedAlert.status}</Tag></p>
              <p><strong>Severity:</strong> <Tag color={getSeverityColor(selectedAlert.severity)}>{selectedAlert.severity}</Tag></p>
            </Card>

            <Row gutter={16}>
              <Col span={12}>
                <Card title="History Timeline" size="small">
                  <Timeline>
                    {alertHistory.map((item) => (
                      <Timeline.Item
                        key={item.id}
                        color={item.action === 'Created' ? 'blue' : item.action === 'Resolved' ? 'green' : 'orange'}
                      >
                        <div>
                          <strong>{item.action}</strong>
                          <br />
                          <small>{new Date(item.timestamp).toLocaleString()}</small>
                          {item.description && (
                            <div style={{ marginTop: '4px', fontSize: '12px', color: '#666' }}>
                              {item.description}
                            </div>
                          )}
                        </div>
                      </Timeline.Item>
                    ))}
                  </Timeline>
                </Card>
              </Col>
              <Col span={12}>
                <Card title="Comments" size="small">
                  <div style={{ maxHeight: '300px', overflowY: 'auto' }}>
                    {alertComments.map((comment) => (
                      <div key={comment.id} style={{ marginBottom: '12px', padding: '8px', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', marginBottom: '4px' }}>
                          <Avatar size="small" icon={<UserOutlined />} />
                          <span style={{ marginLeft: '8px', fontWeight: 'bold' }}>{comment.createdBy}</span>
                          <span style={{ marginLeft: '8px', fontSize: '12px', color: '#666' }}>
                            {new Date(comment.createdAt).toLocaleString()}
                          </span>
                        </div>
                        <div>{comment.comment}</div>
                        {comment.commentType !== 'General' && (
                          <Tag style={{ marginTop: '4px' }}>{comment.commentType}</Tag>
                        )}
                      </div>
                    ))}
                  </div>
                  <Divider />
                  <Form layout="inline">
                    <Form.Item style={{ flex: 1 }}>
                      <Input.TextArea
                        placeholder="Add a comment..."
                        rows={2}
                        onPressEnter={(e) => {
                          e.preventDefault();
                          const value = e.currentTarget.value.trim();
                          if (value && selectedAlert) {
                            handleAddComment(selectedAlert.id, value);
                            e.currentTarget.value = '';
                          }
                        }}
                      />
                    </Form.Item>
                    <Form.Item>
                      <Button
                        type="primary"
                        icon={<CommentOutlined />}
                        onClick={() => {
                          const textarea = document.querySelector('textarea[placeholder="Add a comment..."]') as HTMLTextAreaElement;
                          const value = textarea?.value.trim();
                          if (value && selectedAlert) {
                            handleAddComment(selectedAlert.id, value);
                            textarea.value = '';
                          }
                        }}
                      >
                        Add
                      </Button>
                    </Form.Item>
                  </Form>
                </Card>
              </Col>
            </Row>
          </div>
        )}
      </Modal>
    </div>
  );
};

export default Alerts;