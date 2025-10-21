import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../types/auth';

interface AdminProtectedRouteProps {
  children: React.ReactNode;
}

const AdminProtectedRoute: React.FC<AdminProtectedRouteProps> = ({ children }) => {
  const { user, isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) {
    return (
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100vh',
        fontSize: '16px'
      }}>
        Loading...
      </div>
    );
  }

  if (!isAuthenticated) {
    // Redirect to login page with return url
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (user?.role !== 'Administrator') {
    // Redirect to dashboard if not an administrator
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
};

export default AdminProtectedRoute;