import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { Form, Input, Button, Card, message, Typography, Space, Select } from 'antd';
import { UserOutlined, LockOutlined, MailOutlined } from '@ant-design/icons';
import { useAuth } from '../types/auth';

const { Title } = Typography;
const { Option } = Select;

interface RegisterForm {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  role?: string;
}

const Register: React.FC = () => {
  const [loading, setLoading] = useState(false);
  const { register } = useAuth();
  const navigate = useNavigate();

  const onFinish = async (values: RegisterForm) => {
    if (values.password !== values.confirmPassword) {
      message.error('Passwords do not match!');
      return;
    }

    setLoading(true);
    try {
      await register(values.username, values.email, values.password, values.role || 'User');
      message.success('Registration successful! You are now logged in.');
      navigate('/dashboard');
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      minHeight: '100vh',
      background: '#f0f2f5',
      padding: '20px'
    }}>
      <Card
        style={{
          width: '100%',
          maxWidth: '400px',
          boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)'
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: '24px' }}>
          <Title level={2} style={{ margin: 0, color: '#1890ff' }}>
            SuperPanel
          </Title>
          <p style={{ color: '#666', margin: '8px 0 0 0' }}>
            Create Your Account
          </p>
        </div>

        <Form
          name="register"
          onFinish={onFinish}
          autoComplete="off"
          size="large"
        >
          <Form.Item
            name="username"
            rules={[
              { required: true, message: 'Please input your username!' },
              { min: 3, message: 'Username must be at least 3 characters!' },
              { pattern: /^[a-zA-Z0-9_]+$/, message: 'Username can only contain letters, numbers, and underscores!' }
            ]}
          >
            <Input
              prefix={<UserOutlined />}
              placeholder="Username"
              autoFocus
            />
          </Form.Item>

          <Form.Item
            name="email"
            rules={[
              { required: true, message: 'Please input your email!' },
              { type: 'email', message: 'Please enter a valid email!' }
            ]}
          >
            <Input
              prefix={<MailOutlined />}
              placeholder="Email"
              type="email"
            />
          </Form.Item>

          <Form.Item
            name="password"
            rules={[
              { required: true, message: 'Please input your password!' },
              { min: 6, message: 'Password must be at least 6 characters!' }
            ]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder="Password"
            />
          </Form.Item>

          <Form.Item
            name="confirmPassword"
            rules={[
              { required: true, message: 'Please confirm your password!' }
            ]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder="Confirm Password"
            />
          </Form.Item>

          <Form.Item
            name="role"
            initialValue="User"
          >
            <Select placeholder="Select role">
              <Option value="User">User</Option>
              <Option value="Admin">Admin</Option>
            </Select>
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              block
              style={{ height: '48px', fontSize: '16px' }}
            >
              {loading ? 'Creating Account...' : 'Create Account'}
            </Button>
          </Form.Item>
        </Form>

        <div style={{ textAlign: 'center' }}>
          <Space>
            <span style={{ color: '#666' }}>Already have an account?</span>
            <Link to="/login" style={{ color: '#1890ff' }}>
              Sign in
            </Link>
          </Space>
        </div>
      </Card>
    </div>
  );
};

export default Register;