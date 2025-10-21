import React from 'react';
import { Layout, Menu } from 'antd';
import type { MenuProps } from 'antd';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../types/auth';
import {
  DashboardOutlined,
  CloudServerOutlined,
  GlobalOutlined,
  FolderOutlined,
  DatabaseOutlined,
  UserOutlined,
  SettingOutlined,
  BarChartOutlined,
  SafetyCertificateOutlined,
  MailOutlined,
  CloudUploadOutlined,
  BellOutlined,
} from '@ant-design/icons';

const { Sider } = Layout;

interface SidebarProps {
  collapsed: boolean;
  setCollapsed: (collapsed: boolean) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ collapsed, setCollapsed }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();

  const isAdmin = user?.role === 'Administrator';

  const menuItems: MenuProps['items'] = [
    {
      key: '/',
      icon: <DashboardOutlined />,
      label: 'Dashboard',
    },
    {
      key: '/servers',
      icon: <CloudServerOutlined />,
      label: 'Servers',
    },
    {
      key: '/domains',
      icon: <GlobalOutlined />,
      label: 'Domains',
    },
    {
      key: '/emails',
      icon: <MailOutlined />,
      label: 'Emails',
    },
    {
      key: '/files',
      icon: <FolderOutlined />,
      label: 'File Manager',
    },
    {
      key: '/databases',
      icon: <DatabaseOutlined />,
      label: 'Databases',
    },
    {
      key: '/ssl-certificates',
      icon: <SafetyCertificateOutlined />,
      label: 'SSL Certificates',
    },
    {
      key: '/monitoring',
      icon: <BarChartOutlined />,
      label: 'Monitoring',
    },
    {
      key: '/backups',
      icon: <CloudUploadOutlined />,
      label: 'Backups',
    },
    {
      key: '/alerts',
      icon: <BellOutlined />,
      label: 'Alerts',
    },
    {
      type: 'divider',
    },
    ...(isAdmin ? [{
      key: '/users',
      icon: <UserOutlined />,
      label: 'Users',
    }] : []),
    {
      key: '/settings',
      icon: <SettingOutlined />,
      label: 'Settings',
    },
  ];

  const handleMenuClick: MenuProps['onClick'] = ({ key }) => {
    navigate(key);
  };

  return (
    <Sider
      collapsible
      collapsed={collapsed}
      onCollapse={setCollapsed}
      style={{
        overflow: 'auto',
        height: '100vh',
        position: 'fixed',
        left: 0,
        top: 0,
        bottom: 0,
      }}
    >
      <div
        style={{
          height: 32,
          margin: 16,
          background: 'rgba(255, 255, 255, 0.3)',
          borderRadius: 4,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {!collapsed && (
          <span style={{ color: 'white', fontWeight: 'bold' }}>SuperPanel</span>
        )}
      </div>

      <Menu
        theme="dark"
        defaultSelectedKeys={['/']}
        selectedKeys={[location.pathname]}
        mode="inline"
        items={menuItems}
        onClick={handleMenuClick}
      />
    </Sider>
  );
};

export default Sidebar;