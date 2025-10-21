import { useState } from 'react'
import { Routes, Route } from 'react-router-dom'
import { Layout } from 'antd'
import { AuthProvider } from './contexts/AuthContext'
import ProtectedRoute from './components/ProtectedRoute'
import AdminProtectedRoute from './components/AdminProtectedRoute'
import Sidebar from './components/Layout/Sidebar'
import Header from './components/Layout/Header'
import Login from './pages/Login'
import Register from './pages/Register'
import Dashboard from './pages/Dashboard'
import Servers from './pages/Servers'
import Domains from './pages/Domains'
import Databases from './pages/Databases'
import FileManager from './pages/FileManager'
import Monitoring from './pages/Monitoring'
import Users from './pages/Users'
import SslCertificates from './pages/SslCertificates'
import Emails from './pages/Emails'
import Backups from './pages/Backups'
import Alerts from './pages/Alerts'

const { Content } = Layout

function AppContent() {
  const [collapsed, setCollapsed] = useState(false);

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sidebar collapsed={collapsed} setCollapsed={setCollapsed} />
      <Layout style={{ marginLeft: collapsed ? 80 : 200, transition: 'margin-left 0.2s' }}>
        <Header collapsed={collapsed} setCollapsed={setCollapsed} />
        <Content style={{
          padding: '24px',
          backgroundColor: '#f0f2f5',
          minHeight: 'calc(100vh - 64px)'
        }}>
          <Routes>
            <Route path="/" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
            <Route path="/dashboard" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
            <Route path="/servers" element={<ProtectedRoute><Servers /></ProtectedRoute>} />
            <Route path="/domains" element={<ProtectedRoute><Domains /></ProtectedRoute>} />
            <Route path="/emails" element={<ProtectedRoute><Emails /></ProtectedRoute>} />
            <Route path="/databases" element={<ProtectedRoute><Databases /></ProtectedRoute>} />
            <Route path="/files" element={<ProtectedRoute><FileManager /></ProtectedRoute>} />
            <Route path="/monitoring" element={<ProtectedRoute><Monitoring /></ProtectedRoute>} />
            <Route path="/backups" element={<ProtectedRoute><Backups /></ProtectedRoute>} />
            <Route path="/alerts" element={<ProtectedRoute><Alerts /></ProtectedRoute>} />
            <Route path="/users" element={<AdminProtectedRoute><Users /></AdminProtectedRoute>} />
            <Route path="/ssl-certificates" element={<ProtectedRoute><SslCertificates /></ProtectedRoute>} />
          </Routes>
        </Content>
      </Layout>
    </Layout>
  )
}

function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/*" element={<AppContent />} />
      </Routes>
    </AuthProvider>
  )
}

export default App