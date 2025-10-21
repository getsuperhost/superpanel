import React, { useState } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { Form, Input, Button, Card, message, Typography, Space } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import { useAuth } from '../types/auth';

const { Title } = Typography;

interface LoginForm {
  username: string;
  password: string;
}

const Login: React.FC = () => {
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const from = location.state?.from?.pathname || '/dashboard';

  const onFinish = async (values: LoginForm) => {
    setLoading(true);
    try {
      await login(values.username, values.password);
      message.success('Login successful!');
      navigate(from, { replace: true });
    } catch (error) {
      message.error(error instanceof Error ? error.message : 'Login failed');
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
            Web Hosting Control Panel
          </p>
        </div>

        <Form
          name="login"
          onFinish={onFinish}
          autoComplete="off"
          size="large"
        >
          <Form.Item
            name="username"
            rules={[{ required: true, message: 'Please input your username!' }]}
          >
            <Input
              prefix={<UserOutlined />}
              placeholder="Username"
              autoFocus
            />
          </Form.Item>

          <Form.Item
            name="password"
            rules={[{ required: true, message: 'Please input your password!' }]}
          >
            <Input.Password
              prefix={<LockOutlined />}
              placeholder="Password"
            />
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              loading={loading}
              block
              style={{ height: '48px', fontSize: '16px' }}
            >
              {loading ? 'Signing In...' : 'Sign In'}
            </Button>
          </Form.Item>
        </Form>

        <div style={{ textAlign: 'center' }}>
          <Space>
            <span style={{ color: '#666' }}>Don't have an account?</span>
            <Link to="/register" style={{ color: '#1890ff' }}>
              Sign up
            </Link>
          </Space>
        </div>
      </Card>
    </div>
  );
};

export default Login;