import React, { useState, useEffect, useCallback } from 'react';
import {
  Card,
  Typography,
  Table,
  Button,
  Breadcrumb,
  Modal,
  Input,
  Form,
  message,
  Space,
  Dropdown,
  Tag,
  Spin
} from 'antd';
import {
  FolderOutlined,
  FileOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  FolderAddOutlined,
  ArrowLeftOutlined,
  MoreOutlined,
  EyeOutlined,
  CopyOutlined,
  ScissorOutlined,
  FileTextOutlined
} from '@ant-design/icons';
import { fileApi, FileSystemItem } from '../services/api';

const { Title } = Typography;
const { TextArea } = Input;

interface FileManagerState {
  currentPath: string;
  items: FileSystemItem[];
  loading: boolean;
  selectedItems: string[];
  clipboardItem: { path: string; operation: 'copy' | 'cut' } | null;
}

const FileManager: React.FC = () => {
  const [state, setState] = useState<FileManagerState>({
    currentPath: '/',
    items: [],
    loading: false,
    selectedItems: [],
    clipboardItem: null
  });

  const [fileModal, setFileModal] = useState<{
    visible: boolean;
    mode: 'view' | 'edit' | 'create';
    filePath?: string;
    content?: string;
    loading?: boolean;
  }>({
    visible: false,
    mode: 'view'
  });

  const [directoryModal, setDirectoryModal] = useState<{
    visible: boolean;
    loading?: boolean;
  }>({
    visible: false
  });

  const [form] = Form.useForm();

  const loadDirectoryContents = useCallback(async () => {
    setState(prev => ({ ...prev, loading: true }));
    try {
      const items = await fileApi.browse(state.currentPath);
      setState(prev => ({ ...prev, items, loading: false, selectedItems: [] }));
    } catch (error) {
      console.error('Failed to load directory contents:', error);
      message.error('Failed to load directory contents');
      setState(prev => ({ ...prev, loading: false }));
    }
  }, [state.currentPath]);

  useEffect(() => {
    loadDirectoryContents();
  }, [loadDirectoryContents]);

  const handleNavigate = (path: string) => {
    setState(prev => ({ ...prev, currentPath: path }));
  };

  const handleGoUp = () => {
    if (state.currentPath !== '/') {
      const parentPath = state.currentPath.split('/').slice(0, -1).join('/') || '/';
      handleNavigate(parentPath);
    }
  };

  const handleItemClick = (item: FileSystemItem) => {
    if (item.isDirectory) {
      handleNavigate(item.fullPath);
    } else {
      // Open file for viewing/editing
      openFileModal(item.fullPath, 'view');
    }
  };

  const openFileModal = async (filePath: string, mode: 'view' | 'edit' | 'create') => {
    setFileModal({ visible: true, mode, filePath, loading: true });

    if (mode !== 'create') {
      try {
        const response = await fileApi.readFile(filePath);
        setFileModal(prev => ({
          ...prev,
          content: response.content,
          loading: false
        }));
      } catch (error) {
        console.error('Failed to read file:', error);
        message.error('Failed to read file');
        setFileModal({ visible: false, mode: 'view' });
      }
    } else {
      setFileModal(prev => ({ ...prev, content: '', loading: false }));
    }
  };

  const handleFileSave = async () => {
    if (!fileModal.filePath || !fileModal.content) return;

    try {
      await fileApi.writeFile(fileModal.filePath, fileModal.content!);
      message.success('File saved successfully');
      setFileModal({ visible: false, mode: 'view' });
      loadDirectoryContents();
    } catch (error) {
      console.error('Failed to save file:', error);
      message.error('Failed to save file');
    }
  };

  const handleCreateDirectory = async (values: { name: string }) => {
    const newPath = state.currentPath === '/' ? `/${values.name}` : `${state.currentPath}/${values.name}`;

    try {
      await fileApi.createDirectory(newPath);
      message.success('Directory created successfully');
      setDirectoryModal({ visible: false });
      form.resetFields();
      loadDirectoryContents();
    } catch (error) {
      console.error('Failed to create directory:', error);
      message.error('Failed to create directory');
    }
  };

  const handleDelete = async (path: string) => {
    Modal.confirm({
      title: 'Delete Item',
      content: `Are you sure you want to delete "${path.split('/').pop()}"?`,
      okType: 'danger',
      onOk: async () => {
        try {
          if (path.endsWith('/')) {
            await fileApi.deleteDirectory(path);
          } else {
            await fileApi.deleteFile(path);
          }
          message.success('Item deleted successfully');
          loadDirectoryContents();
        } catch (error) {
          console.error('Failed to delete item:', error);
          message.error('Failed to delete item');
        }
      }
    });
  };

  const handleCopy = (path: string) => {
    setState(prev => ({ ...prev, clipboardItem: { path, operation: 'copy' } }));
    message.info('Item copied to clipboard');
  };

  const handleCut = (path: string) => {
    setState(prev => ({ ...prev, clipboardItem: { path, operation: 'cut' } }));
    message.info('Item cut to clipboard');
  };

  const handlePaste = async () => {
    if (!state.clipboardItem) return;

    const fileName = state.clipboardItem.path.split('/').pop() || '';
    const destinationPath = state.currentPath === '/' ? `/${fileName}` : `${state.currentPath}/${fileName}`;

    try {
      if (state.clipboardItem.operation === 'copy') {
        await fileApi.copy(state.clipboardItem.path, destinationPath);
        message.success('Item copied successfully');
      } else {
        await fileApi.move(state.clipboardItem.path, destinationPath);
        message.success('Item moved successfully');
        setState(prev => ({ ...prev, clipboardItem: null }));
      }
      loadDirectoryContents();
    } catch (error) {
      console.error('Failed to paste item:', error);
      message.error('Failed to paste item');
    }
  };

  const getBreadcrumbItems = () => {
    const paths = state.currentPath.split('/').filter(p => p);
    const items = [{ title: 'Root', onClick: () => handleNavigate('/') }];

    let currentPath = '';
    paths.forEach((path) => {
      currentPath += `/${path}`;
      items.push({
        title: path,
        onClick: () => handleNavigate(currentPath)
      });
    });

    return items;
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const columns = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string, item: FileSystemItem) => (
        <Space>
          {item.isDirectory ? <FolderOutlined style={{ color: '#1890ff' }} /> : <FileOutlined />}
          <a onClick={() => handleItemClick(item)}>{name}</a>
        </Space>
      ),
      sorter: (a: FileSystemItem, b: FileSystemItem) => a.name.localeCompare(b.name)
    },
    {
      title: 'Size',
      dataIndex: 'sizeBytes',
      key: 'size',
      render: (size: number, item: FileSystemItem) => (
        item.isDirectory ? '-' : formatFileSize(size)
      ),
      sorter: (a: FileSystemItem, b: FileSystemItem) => a.sizeBytes - b.sizeBytes
    },
    {
      title: 'Modified',
      dataIndex: 'lastModified',
      key: 'lastModified',
      render: (date: string) => new Date(date).toLocaleString(),
      sorter: (a: FileSystemItem, b: FileSystemItem) => new Date(a.lastModified).getTime() - new Date(b.lastModified).getTime()
    },
    {
      title: 'Permissions',
      dataIndex: 'permissions',
      key: 'permissions',
      render: (permissions: string) => <Tag>{permissions}</Tag>
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_text: string, item: FileSystemItem) => (
        <Dropdown
          menu={{
            items: [
              {
                key: 'view',
                icon: <EyeOutlined />,
                label: 'View',
                onClick: () => openFileModal(item.fullPath, 'view')
              },
              ...(item.isDirectory ? [] : [{
                key: 'edit',
                icon: <EditOutlined />,
                label: 'Edit',
                onClick: () => openFileModal(item.fullPath, 'edit')
              }]),
              {
                key: 'copy',
                icon: <CopyOutlined />,
                label: 'Copy',
                onClick: () => handleCopy(item.fullPath)
              },
              {
                key: 'cut',
                icon: <ScissorOutlined />,
                label: 'Cut',
                onClick: () => handleCut(item.fullPath)
              },
              {
                key: 'delete',
                icon: <DeleteOutlined />,
                label: 'Delete',
                danger: true,
                onClick: () => handleDelete(item.fullPath)
              }
            ]
          }}
          trigger={['click']}
        >
          <Button icon={<MoreOutlined />} />
        </Dropdown>
      )
    }
  ];

  return (
    <div>
      <Title level={2}>
        <FileTextOutlined /> File Manager
      </Title>

      <Card>
        {/* Navigation */}
        <Space direction="vertical" style={{ width: '100%', marginBottom: '16px' }}>
          <Space>
            <Button
              icon={<ArrowLeftOutlined />}
              onClick={handleGoUp}
              disabled={state.currentPath === '/'}
            >
              Up
            </Button>
            <Breadcrumb items={getBreadcrumbItems()} />
          </Space>

          <Space>
            <Button
              icon={<PlusOutlined />}
              onClick={() => openFileModal('', 'create')}
            >
              New File
            </Button>
            <Button
              icon={<FolderAddOutlined />}
              onClick={() => setDirectoryModal({ visible: true })}
            >
              New Folder
            </Button>
            {state.clipboardItem && (
              <Button onClick={handlePaste}>
                Paste
              </Button>
            )}
          </Space>
        </Space>

        {/* File Table */}
        <Table
          columns={columns}
          dataSource={state.items}
          rowKey="fullPath"
          loading={state.loading}
          pagination={false}
          size="small"
          scroll={{ y: 400 }}
        />
      </Card>

      {/* File Modal */}
      <Modal
        title={
          fileModal.mode === 'create' ? 'Create New File' :
          fileModal.mode === 'edit' ? 'Edit File' : 'View File'
        }
        open={fileModal.visible}
        onCancel={() => setFileModal({ visible: false, mode: 'view' })}
        onOk={fileModal.mode === 'edit' ? handleFileSave : undefined}
        okText={fileModal.mode === 'edit' ? 'Save' : 'OK'}
        width={800}
      >
        {fileModal.mode === 'create' ? (
          <Form layout="vertical">
            <Form.Item label="File Name" required>
              <Input
                placeholder="Enter file name"
                onChange={(e) => {
                  const fileName = e.target.value;
                  const filePath = state.currentPath === '/' ? `/${fileName}` : `${state.currentPath}/${fileName}`;
                  setFileModal(prev => ({ ...prev, filePath }));
                }}
              />
            </Form.Item>
          </Form>
        ) : (
          <div>
            <div style={{ marginBottom: '16px' }}>
              <strong>File:</strong> {fileModal.filePath}
            </div>
            {fileModal.loading ? (
              <Spin />
            ) : (
              <TextArea
                value={fileModal.content}
                onChange={(e) => setFileModal(prev => ({ ...prev, content: e.target.value }))}
                readOnly={fileModal.mode === 'view'}
                rows={20}
                style={{ fontFamily: 'monospace' }}
              />
            )}
          </div>
        )}
      </Modal>

      {/* Directory Modal */}
      <Modal
        title="Create New Directory"
        open={directoryModal.visible}
        onCancel={() => setDirectoryModal({ visible: false })}
        onOk={() => form.submit()}
        okText="Create"
      >
        <Form form={form} layout="vertical" onFinish={handleCreateDirectory}>
          <Form.Item
            name="name"
            label="Directory Name"
            rules={[{ required: true, message: 'Please enter directory name' }]}
          >
            <Input placeholder="Enter directory name" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default FileManager;