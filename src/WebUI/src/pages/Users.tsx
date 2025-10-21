import React, { useState, useEffect } from 'react';
import { Table, Card, Tag, Alert, Spin, Button, Modal, Form, Input, Select, message, Popconfirm } from 'antd';
import { User as UserType } from '../types/users';
import { userApi } from '../services/api';

const { Option } = Select;

interface UserFormData {
  username: string;
  email: string;
  password?: string;
  role: string;
}

interface CreateUserFormData extends Omit<UserFormData, 'password'> {
  password: string;
}

const Users: React.FC = () => {
  const [users, setUsers] = useState<UserType[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isCreateModalVisible, setIsCreateModalVisible] = useState(false);
  const [isEditModalVisible, setIsEditModalVisible] = useState(false);
  const [editingUser, setEditingUser] = useState<UserType | null>(null);
  const [form] = Form.useForm();

  useEffect(() => {
    fetchUsers();
  }, []);

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const data = await userApi.getAll();
      setUsers(data);
      setError(null);
    } catch (err) {
      setError('Failed to load users. You may not have admin privileges.');
      console.error('Error fetching users:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (values: CreateUserFormData) => {
    try {
      await userApi.create(values);
      message.success('User created successfully');
      setIsCreateModalVisible(false);
      form.resetFields();
      fetchUsers();
    } catch (err) {
      message.error('Failed to create user');
      console.error('Error creating user:', err);
    }
  };

  const handleEdit = async (values: Partial<UserFormData>) => {
    if (!editingUser) return;
    try {
      await userApi.update(editingUser.id, values);
      message.success('User updated successfully');
      setIsEditModalVisible(false);
      setEditingUser(null);
      form.resetFields();
      fetchUsers();
    } catch (err) {
      message.error('Failed to update user');
      console.error('Error updating user:', err);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await userApi.delete(id);
      message.success('User deleted successfully');
      fetchUsers();
    } catch (err) {
      message.error('Failed to delete user');
      console.error('Error deleting user:', err);
    }
  };

  const openCreateModal = () => {
    setIsCreateModalVisible(true);
  };

  const openEditModal = (user: UserType) => {
    setEditingUser(user);
    form.setFieldsValue(user);
    setIsEditModalVisible(true);
  };

  const closeCreateModal = () => {
    setIsCreateModalVisible(false);
    form.resetFields();
  };

  const closeEditModal = () => {
    setIsEditModalVisible(false);
    setEditingUser(null);
    form.resetFields();
  };

  const columns = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: 'Username',
      dataIndex: 'username',
      key: 'username',
    },
    {
      title: 'Email',
      dataIndex: 'email',
      key: 'email',
    },
    {
      title: 'Role',
      dataIndex: 'role',
      key: 'role',
      render: (role: string) => (
        <Tag color={role === 'Administrator' ? 'red' : 'blue'}>
          {role}
        </Tag>
      ),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_: unknown, record: UserType) => (
        <>
          <Button type="link" onClick={() => openEditModal(record)}>
            Edit
          </Button>
          <Popconfirm
            title="Are you sure you want to delete this user?"
            onConfirm={() => handleDelete(record.id)}
            okText="Yes"
            cancelText="No"
          >
            <Button type="link" danger>
              Delete
            </Button>
          </Popconfirm>
        </>
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

  return (
    <div>
      <h1>User Management</h1>
      <Button type="primary" onClick={openCreateModal} style={{ marginBottom: 16 }}>
        Create User
      </Button>
      {error && (
        <Alert
          message="Error"
          description={error}
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
        />
      )}
      <Card>
        <Table
          columns={columns}
          dataSource={users}
          rowKey="id"
          pagination={false}
        />
      </Card>

      {/* Create User Modal */}
      <Modal
        title="Create User"
        open={isCreateModalVisible}
        onCancel={closeCreateModal}
        footer={null}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleCreate}
        >
          <Form.Item
            name="username"
            label="Username"
            rules={[{ required: true, message: 'Please enter username' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="email"
            label="Email"
            rules={[
              { required: true, message: 'Please enter email' },
              { type: 'email', message: 'Please enter a valid email' }
            ]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="password"
            label="Password"
            rules={[{ required: true, message: 'Please enter password' }]}
          >
            <Input.Password />
          </Form.Item>
          <Form.Item
            name="role"
            label="Role"
            rules={[{ required: true, message: 'Please select role' }]}
          >
            <Select>
              <Option value="User">User</Option>
              <Option value="Administrator">Administrator</Option>
            </Select>
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit">
              Create
            </Button>
            <Button onClick={closeCreateModal} style={{ marginLeft: 8 }}>
              Cancel
            </Button>
          </Form.Item>
        </Form>
      </Modal>

      {/* Edit User Modal */}
      <Modal
        title="Edit User"
        open={isEditModalVisible}
        onCancel={closeEditModal}
        footer={null}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleEdit}
        >
          <Form.Item
            name="username"
            label="Username"
            rules={[{ required: true, message: 'Please enter username' }]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="email"
            label="Email"
            rules={[
              { required: true, message: 'Please enter email' },
              { type: 'email', message: 'Please enter a valid email' }
            ]}
          >
            <Input />
          </Form.Item>
          <Form.Item
            name="password"
            label="Password (leave empty to keep current)"
          >
            <Input.Password />
          </Form.Item>
          <Form.Item
            name="role"
            label="Role"
            rules={[{ required: true, message: 'Please select role' }]}
          >
            <Select>
              <Option value="User">User</Option>
              <Option value="Administrator">Administrator</Option>
            </Select>
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit">
              Update
            </Button>
            <Button onClick={closeEditModal} style={{ marginLeft: 8 }}>
              Cancel
            </Button>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default Users;