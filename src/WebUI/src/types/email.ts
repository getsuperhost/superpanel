// Email Management Types
export interface EmailAccount {
  id: number;
  emailAddress: string;
  username: string;
  domainId: number;
  domain?: {
    id: number;
    name: string;
  };
  quotaMB: number;
  usedQuotaMB: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  forwarders?: EmailForwarder[];
  aliases?: EmailAlias[];
}

export interface EmailForwarder {
  id: number;
  emailAccountId: number;
  forwardTo: string;
  isActive: boolean;
  createdAt: string;
}

export interface EmailAlias {
  id: number;
  emailAccountId: number;
  aliasAddress: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateEmailAccountRequest {
  emailAddress: string;
  username: string;
  password: string;
  domainId: number;
  quotaMB: number;
}

export interface UpdateEmailAccountRequest {
  quotaMB?: number;
  isActive?: boolean;
  newPassword?: string;
}

export interface CreateForwarderRequest {
  forwardTo: string;
}

export interface CreateAliasRequest {
  aliasAddress: string;
}

export interface EmailStats {
  totalAccounts: number;
  activeAccounts: number;
  totalForwarders: number;
  totalAliases: number;
  totalQuotaMB: number;
  usedQuotaMB: number;
}