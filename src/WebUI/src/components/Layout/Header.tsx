import React from 'react';
import { Layout, Avatar, Dropdown, Space } from 'antd';
import type { MenuProps } from 'antd';
import { useAuth } from '../../types/auth';
import { useNavigate } from 'react-router-dom';
import {
  UserOutlined,
  LogoutOutlined,
  SettingOutlined,
  BellOutlined
} from '@ant-design/icons';

const { Header: AntHeader } = Layout;

interface HeaderProps {
  collapsed: boolean;
  setCollapsed: (collapsed: boolean) => void;
}

const Header: React.FC<HeaderProps> = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const userMenuItems: MenuProps['items'] = [
    {
      key: 'profile',
      icon: <UserOutlined />,
      label: 'Profile',
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: 'Settings',
    },
    {
      type: 'divider',
    },
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: 'Logout',
      danger: true,
    },
  ];

  const handleUserMenuClick: MenuProps['onClick'] = ({ key }) => {
    switch (key) {
      case 'logout':
        logout();
        navigate('/login');
        break;
      case 'profile':
        // Navigate to profile
        break;
      case 'settings':
        // Navigate to settings
        break;
    }
  };

  return (
    <AntHeader
      style={{
        padding: '0 16px',
        background: '#fff',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        boxShadow: '0 1px 4px rgba(0,21,41,.08)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center' }}>
        <h2 style={{ margin: 0, color: '#1890ff' }}>SuperPanel</h2>
      </div>

      <Space size="middle">
        <BellOutlined style={{ fontSize: '18px', cursor: 'pointer' }} />

        <Dropdown
          menu={{
            items: userMenuItems,
            onClick: handleUserMenuClick,
          }}
          placement="bottomRight"
        >
          <Space style={{ cursor: 'pointer' }}>
            <Avatar size="small" icon={<UserOutlined />} />
            <span>{user?.username || 'User'}</span>
          </Space>
        </Dropdown>
      </Space>
    </AntHeader>
  );
};

export default Header;