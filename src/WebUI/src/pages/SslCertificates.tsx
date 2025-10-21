import React, { useState, useEffect } from 'react';
import { Table, Button, Modal, Form, Input, Select, Tag, Space, message, Card, Statistic, Alert } from 'antd';
import { PlusOutlined, ReloadOutlined, CheckCircleOutlined, ExclamationCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { sslCertificateApi, domainApi, Domain } from '../services/api';
import { SslCertificate, CertificateStatus, CertificateType, CertificateRequest } from '../types';

const SslCertificates: React.FC = () => {
  const [certificates, setCertificates] = useState<SslCertificate[]>([]);
  const [expiringSoon, setExpiringSoon] = useState<SslCertificate[]>([]);
  const [domains, setDomains] = useState<Domain[]>([]);
  const [loading, setLoading] = useState(false);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    loadCertificates();
    loadExpiringSoon();
    loadDomains();
  }, []);

  const loadCertificates = async () => {
    try {
      setLoading(true);
      const response = await sslCertificateApi.getAll();
      setCertificates(response);
    } catch (error) {
      console.error('Failed to load SSL certificates:', error);
      message.error('Failed to load SSL certificates');
    } finally {
      setLoading(false);
    }
  };

  const loadExpiringSoon = async () => {
    try {
      const response = await sslCertificateApi.getExpiringSoon();
      setExpiringSoon(response);
    } catch (error) {
      console.error('Failed to load expiring certificates:', error);
    }
  };

  const loadDomains = async () => {
    try {
      const response = await domainApi.getAll();
      setDomains(response);
    } catch (error) {
      console.error('Failed to load domains:', error);
      message.error('Failed to load domains');
    }
  };

  const handleRequestCertificate = async (values: {
    domainId: number;
    type: CertificateType;
    autoRenew: boolean;
    email?: string;
    validationMethod?: string;
    notes?: string;
  }) => {
    try {
      const request: CertificateRequest = {
        domainId: values.domainId,
        type: values.type,
        autoRenew: values.autoRenew,
        email: values.email,
      };
      await sslCertificateApi.request(request);
      message.success('SSL certificate request submitted successfully');
      setIsModalVisible(false);
      form.resetFields();
      loadCertificates();
    } catch (error) {
      console.error('Failed to request SSL certificate:', error);
      message.error('Failed to request SSL certificate');
    }
  };

  const handleRenewCertificate = async (certificateId: number) => {
    try {
      await sslCertificateApi.renew(certificateId);
      message.success('Certificate renewal initiated');
      loadCertificates();
    } catch (error) {
      console.error('Failed to renew certificate:', error);
      message.error('Failed to renew certificate');
    }
  };

  const handleDeleteCertificate = async (certificateId: number) => {
    try {
      await sslCertificateApi.delete(certificateId);
      message.success('Certificate deleted successfully');
      loadCertificates();
      loadExpiringSoon();
    } catch (error) {
      console.error('Failed to delete certificate:', error);
      message.error('Failed to delete certificate');
    }
  };

  const getStatusColor = (status: CertificateStatus) => {
    switch (status) {
      case CertificateStatus.Active: return 'green';
      case CertificateStatus.Pending: return 'orange';
      case CertificateStatus.Expired: return 'red';
      case CertificateStatus.Revoked: return 'red';
      case CertificateStatus.Failed: return 'red';
      default: return 'default';
    }
  };

  const getStatusText = (status: CertificateStatus) => {
    switch (status) {
      case CertificateStatus.Active: return 'Active';
      case CertificateStatus.Pending: return 'Pending';
      case CertificateStatus.Expired: return 'Expired';
      case CertificateStatus.Revoked: return 'Revoked';
      case CertificateStatus.Failed: return 'Failed';
      default: return 'Unknown';
    }
  };

  const getTypeText = (type: CertificateType) => {
    switch (type) {
      case CertificateType.DV: return "Let's Encrypt";
      case CertificateType.OV: return 'Organization Validated';
      case CertificateType.EV: return 'Extended Validation';
      case CertificateType.SelfSigned: return 'Self-Signed';
      default: return 'Unknown';
    }
  };

  const isExpiringSoon = (expiresAt: string) => {
    const expiryDate = dayjs(expiresAt);
    const now = dayjs();
    const daysUntilExpiry = expiryDate.diff(now, 'day');
    return daysUntilExpiry <= 30 && daysUntilExpiry > 0;
  };

  const isExpired = (expiresAt: string) => {
    return dayjs(expiresAt).isBefore(dayjs());
  };

  const columns: ColumnsType<SslCertificate> = [
    {
      title: 'Domain',
      dataIndex: 'domainName',
      key: 'domainName',
      render: (text, record) => (
        <div>
          <div style={{ fontWeight: 'bold' }}>{text}</div>
          {record.domain && <div style={{ fontSize: '12px', color: '#666' }}>Domain: {record.domain.name}</div>}
        </div>
      ),
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: CertificateStatus) => (
        <Tag color={getStatusColor(status)}>
          {getStatusText(status)}
        </Tag>
      ),
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      render: (type: CertificateType) => getTypeText(type),
    },
    {
      title: 'Expires',
      dataIndex: 'expiresAt',
      key: 'expiresAt',
      render: (expiresAt: string) => {
        const expired = isExpired(expiresAt);
        const expiringSoon = isExpiringSoon(expiresAt);

        return (
          <div>
            <div style={{
              color: expired ? '#ff4d4f' : expiringSoon ? '#faad14' : 'inherit',
              fontWeight: expired || expiringSoon ? 'bold' : 'normal'
            }}>
              {dayjs(expiresAt).format('YYYY-MM-DD')}
            </div>
            {expired && <div style={{ fontSize: '12px', color: '#ff4d4f' }}>Expired</div>}
            {expiringSoon && !expired && <div style={{ fontSize: '12px', color: '#faad14' }}>Expires Soon</div>}
          </div>
        );
      },
    },
    {
      title: 'Auto Renew',
      dataIndex: 'autoRenew',
      key: 'autoRenew',
      render: (autoRenew: boolean) => autoRenew ? <CheckCircleOutlined style={{ color: '#52c41a' }} /> : <CloseCircleOutlined style={{ color: '#ff4d4f' }} />,
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, record) => (
        <Space size="small">
          {record.status === CertificateStatus.Active && (
            <Button
              size="small"
              onClick={() => handleRenewCertificate(record.id)}
              icon={<ReloadOutlined />}
            >
              Renew
            </Button>
          )}
          <Button
            size="small"
            danger
            onClick={() => handleDeleteCertificate(record.id)}
          >
            Delete
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <h2>SSL Certificates</h2>
        <p>Manage SSL certificates for your domains</p>
      </div>

      {/* Statistics Cards */}
      <div style={{ marginBottom: 16 }}>
        <Space>
          <Card size="small">
            <Statistic
              title="Total Certificates"
              value={certificates.length}
              prefix={<CheckCircleOutlined />}
            />
          </Card>
          <Card size="small">
            <Statistic
              title="Active Certificates"
              value={certificates.filter(c => c.status === CertificateStatus.Active).length}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
          <Card size="small">
            <Statistic
              title="Expiring Soon"
              value={expiringSoon.length}
              prefix={<ExclamationCircleOutlined />}
              valueStyle={{ color: '#faad14' }}
            />
          </Card>
        </Space>
      </div>

      {/* Expiring Soon Alert */}
      {expiringSoon.length > 0 && (
        <Alert
          message={`⚠️ ${expiringSoon.length} certificate(s) expiring within 30 days`}
          description="Please renew these certificates to avoid service interruption."
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}

      {/* Action Buttons */}
      <div style={{ marginBottom: 16 }}>
        <Space>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setIsModalVisible(true)}
          >
            Request Certificate
          </Button>
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              loadCertificates();
              loadExpiringSoon();
            }}
          >
            Refresh
          </Button>
        </Space>
      </div>

      {/* Certificates Table */}
      <Table
        columns={columns}
        dataSource={certificates}
        rowKey="id"
        loading={loading}
        pagination={{
          pageSize: 10,
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total, range) => `${range[0]}-${range[1]} of ${total} certificates`,
        }}
      />

      {/* Request Certificate Modal */}
      <Modal
        title="Request SSL Certificate"
        open={isModalVisible}
        onCancel={() => {
          setIsModalVisible(false);
          form.resetFields();
        }}
        footer={null}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleRequestCertificate}
        >
          <Form.Item
            name="domainId"
            label="Domain"
            rules={[{ required: true, message: 'Please select a domain' }]}
          >
            <Select placeholder="Select a domain">
              {domains.map(domain => (
                <Select.Option key={domain.id} value={domain.id}>
                  {domain.domainName}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="email"
            label="Email Address"
            rules={[
              { required: true, message: 'Please enter an email address' },
              { type: 'email', message: 'Please enter a valid email address' }
            ]}
          >
            <Input placeholder="admin@example.com" />
          </Form.Item>

          <Form.Item
            name="type"
            label="Certificate Type"
            initialValue={CertificateType.DV}
          >
            <Select>
              <Select.Option value={CertificateType.DV}>Domain Validated (DV) - Let's Encrypt</Select.Option>
              <Select.Option value={CertificateType.OV}>Organization Validated (OV)</Select.Option>
              <Select.Option value={CertificateType.EV}>Extended Validation (EV)</Select.Option>
              <Select.Option value={CertificateType.SelfSigned}>Self-Signed (Development)</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="autoRenew"
            label="Auto Renew"
            valuePropName="checked"
            initialValue={true}
          >
            <input type="checkbox" />
          </Form.Item>

          <Form.Item
            name="validationMethod"
            label="Validation Method"
          >
            <Select placeholder="Select validation method">
              <Select.Option value="http-01">HTTP-01 (Recommended)</Select.Option>
              <Select.Option value="dns-01">DNS-01</Select.Option>
            </Select>
          </Form.Item>

          <Form.Item
            name="notes"
            label="Notes"
          >
            <Input.TextArea placeholder="Optional notes about this certificate" />
          </Form.Item>

          <Form.Item style={{ marginBottom: 0, textAlign: 'right' }}>
            <Space>
              <Button onClick={() => {
                setIsModalVisible(false);
                form.resetFields();
              }}>
                Cancel
              </Button>
              <Button type="primary" htmlType="submit">
                Request Certificate
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default SslCertificates;